using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using System.Net.Sockets;
using System.Text;

namespace RacingSim.Telemetry
{
    /// <summary>
    /// Complete telemetry frame for one simulation tick
    /// Optimized for binary serialization and UDP transmission
    /// </summary>
    [BurstCompile]
    public struct TelemetryFrame
    {
        // Header
        public double Timestamp;          // Unix timestamp (ms)
        public int FrameIndex;
        public int SessionType;           // 0=Practice, 1=Quali, 2=Race
        
        // Lap timing
        public int LapNumber;
        public float LapTime;
        public float LastLapTime;
        public float BestLapTime;
        public float Sector1Time, Sector2Time, Sector3Time;
        public float DeltaToBest;
        
        // Vehicle state
        public float3 Position;
        public float Yaw, Pitch, Roll;
        public float Speed;               // km/h
        public float RPM;
        public int Gear;
        public float Throttle;            // 0-1
        public float Brake;               // 0-1
        public float Steering;            // -1 to 1
        public float Clutch;              // 0-1
        
        // G-Forces
        public float GForceLong;
        public float GForceLat;
        public float GForceVert;
        
        // Tires (4 wheels × data)
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TireTempInner;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TireTempMiddle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TireTempOuter;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TirePressure;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] TireWear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] BrakeTemp;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] SlipRatio;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] SlipAngle;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] VerticalLoad;
        
        // Engine
        public float EngineTemp;
        public float OilTemp;
        public float FuelLevel;           // kg
        public float FuelFlow;            // kg/s
        public float FuelRemainingLaps;
        
        // Electronics
        public bool TCActive;
        public float TCReduction;
        public bool ABSActive;
        public float ABSReduction;
        public bool DRSActive;
        public int ActiveShiftLights;
        
        // Damage
        public float EngineHealth;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] SuspensionHealth;
        public float AeroDamageFront;
        public float AeroDamageRear;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public bool[] TirePunctured;
        
        // Track position
        public float DistanceTraveled;
        public float TrackPosition;       // 0-1 along track
        public float DistanceFromRacingLine;
        
        // Weather
        public float AirTemp;
        public float TrackTemp;
        public float RainIntensity;
    }

    /// <summary>
    /// Binary telemetry logger for file output
    /// </summary>
    public static class TelemetryLogger
    {
        private static System.IO.FileStream logFile;
        private static byte[] writeBuffer;
        private static int bufferIndex;
        private const int BUFFER_SIZE = 65536;
        
        /// <summary>
        /// Initialize logger with new file
        /// </summary>
        public static void Initialize(string sessionName)
        {
            string filename = $"telemetry_{sessionName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.bin";
            string fullPath = System.IO.Path.Combine(Application.persistentDataPath, "Telemetry", filename);
            
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            logFile = System.IO.File.Create(fullPath);
            
            writeBuffer = new byte[BUFFER_SIZE];
            bufferIndex = 0;
            
            // Write header
            WriteHeader();
        }
        
        /// <summary>
        /// Write telemetry frame to buffer
        /// </summary>
        public static void LogFrame(ref TelemetryFrame frame)
        {
            if (logFile == null) return;
            
            // Serialize frame to bytes
            int frameSize = System.Runtime.InteropServices.Marshal.SizeOf<TelemetryFrame>();
            
            if (bufferIndex + frameSize > BUFFER_SIZE)
            {
                FlushBuffer();
            }
            
            // Convert struct to bytes (simplified - would need proper serialization in production)
            byte[] frameBytes = SerializeFrame(ref frame);
            
            System.Buffer.BlockCopy(frameBytes, 0, writeBuffer, bufferIndex, frameBytes.Length);
            bufferIndex += frameBytes.Length;
        }
        
        /// <summary>
        /// Flush buffer to disk
        /// </summary>
        public static void FlushBuffer()
        {
            if (logFile == null || bufferIndex == 0) return;
            
            logFile.Write(writeBuffer, 0, bufferIndex);
            bufferIndex = 0;
        }
        
        /// <summary>
        /// Close logger
        /// </summary>
        public static void Shutdown()
        {
            FlushBuffer();
            logFile?.Close();
            logFile = null;
        }
        
        private static void WriteHeader()
        {
            // Magic number + version + timestamp
            byte[] header = Encoding.ASCII.GetBytes("RTELEM01");
            logFile.Write(header, 0, header.Length);
        }
        
        private static byte[] SerializeFrame(ref TelemetryFrame frame)
        {
            // Simplified serialization - in production use proper binary formatter
            // This is placeholder code
            return System.BitConverter.GetBytes(frame.Timestamp);
        }
    }

    /// <summary>
    /// UDP telemetry sender for external dashboards
    /// Sends to specified IP:Port for tools like SimHub, DashStudio, etc.
    /// </summary>
    public class UDPTelemetrySender : System.IDisposable
    {
        private UdpClient udpClient;
        private System.Net.IPEndPoint endPoint;
        private byte[] sendBuffer;
        private bool disposed;
        
        public UDPTelemetrySender(string ipAddress, int port)
        {
            udpClient = new UdpClient();
            endPoint = new System.Net.IPEndPoint(
                System.Net.IPAddress.Parse(ipAddress), 
                port
            );
            sendBuffer = new byte[1024];
            disposed = false;
        }
        
        /// <summary>
        /// Send telemetry frame via UDP
        /// </summary>
        public void Send(ref TelemetryFrame frame)
        {
            if (disposed) return;
            
            try
            {
                // Serialize to compact binary format
                int size = SerializeToUDP(ref frame, ref sendBuffer);
                
                // Send datagram
                udpClient.Send(sendBuffer, size, endPoint);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"UDP Telemetry Error: {e.Message}");
            }
        }
        
        private int SerializeToUDP(ref TelemetryFrame frame, ref byte[] buffer)
        {
            // Compact binary format optimized for UDP
            // Header: magic (4) + frameIndex (4) + timestamp (8) = 16 bytes
            // Data: ~200-300 bytes depending on what's included
            
            int offset = 0;
            
            // Magic number
            uint magic = 0x5254454C; // "RTEL"
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(magic), 0, buffer, offset, 4);
            offset += 4;
            
            // Frame index
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(frame.FrameIndex), 0, buffer, offset, 4);
            offset += 4;
            
            // Timestamp
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(frame.Timestamp), 0, buffer, offset, 8);
            offset += 8;
            
            // Vehicle state (compact)
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(frame.Speed), 0, buffer, offset, 4);
            offset += 4;
            System.Buffer.BlockCopy(System.BitConverter.GetBytes(frame.RPM), 0, buffer, offset, 4);
            offset += 4;
            buffer[offset++] = (byte)(frame.Gear & 0xFF);
            buffer[offset++] = (byte)(frame.Throttle * 255f);
            buffer[offset++] = (byte)(frame.Brake * 255f);
            buffer[offset++] = (byte)((frame.Steering + 1f) * 127.5f);
            
            // Tires (temps only for bandwidth)
            for (int i = 0; i < 4; i++)
            {
                buffer[offset++] = (byte)frame.TireTempMiddle[i];
                buffer[offset++] = (byte)frame.BrakeTemp[i];
            }
            
            // Flags
            byte flags = 0;
            if (frame.TCActive) flags |= 0x01;
            if (frame.ABSActive) flags |= 0x02;
            if (frame.DRSActive) flags |= 0x04;
            buffer[offset++] = flags;
            
            return offset;
        }
        
        public void Dispose()
        {
            if (!disposed)
            {
                udpClient?.Close();
                udpClient?.Dispose();
                disposed = true;
            }
        }
    }

    /// <summary>
    /// Lap timing system with sector tracking
    /// </summary>
    [BurstCompile]
    public static class LapTimingSystem
    {
        private struct SectorTrigger
        {
            public float3 position;
            public float radius;
            public bool crossed;
        }
        
        /// <summary>
        /// Check if vehicle crossed start/finish line
        /// </summary>
        [BurstCompile]
        public static bool CheckStartFinishCrossing(
            float3 previousPosition,
            float3 currentPosition,
            float3 startFinishPoint,
            float3 startFinishNormal,
            out bool isInvalidLap)
        {
            isInvalidLap = false;
            
            // Project positions onto track normal
            float prevDot = math.dot(previousPosition - startFinishPoint, startFinishNormal);
            float currDot = math.dot(currentPosition - startFinishPoint, startFinishNormal);
            
            // Crossing detected when sign changes from negative to positive
            if (prevDot < 0f && currDot >= 0f)
            {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Check sector crossing
        /// </summary>
        [BurstCompile]
        public static bool CheckSectorCrossing(
            float3 currentPosition,
            float3 sectorPosition,
            float sectorRadius,
            ref bool hasCrossed)
        {
            if (hasCrossed) return false;
            
            float distanceSq = math.distancesq(currentPosition, sectorPosition);
            
            if (distanceSq < sectorRadius * sectorRadius)
            {
                hasCrossed = true;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Calculate lap time delta to reference lap
        /// </summary>
        [BurstCompile]
        public static float CalculateDelta(
            float currentLapTime,
            float referenceLapTimeAtSamePoint,
            float trackProgressCurrent,
            float trackProgressReference)
        {
            // Simple delta calculation
            return currentLapTime - referenceLapTimeAtSamePoint;
        }
        
        /// <summary>
        /// Validate lap (no shortcuts, correct direction)
        /// </summary>
        [BurstCompile]
        public static bool ValidateLap(
            int sectorsCrossed,
            float minimumLapTime,
            float currentLapTime,
            bool cutTrackDetected)
        {
            if (sectorsCrossed < 3) return false;
            if (currentLapTime < minimumLapTime) return false;
            if (cutTrackDetected) return false;
            
            return true;
        }
    }

    /// <summary>
    /// In-game HUD data extractor
    /// Provides formatted data for UI display
    /// </summary>
    [BurstCompile]
    public static class HUDDataProvider
    {
        /// <summary>
        /// Format speed for display
        /// </summary>
        [BurstCompile]
        public static string FormatSpeed(float speedKmh, bool useMph = false)
        {
            if (useMph)
            {
                speedKmh *= 0.621371f;
                return $"{speedKmh:F0} MPH";
            }
            return $"{speedKmh:F0} KM/H";
        }
        
        /// <summary>
        /// Format lap time for display
        /// </summary>
        [BurstCompile]
        public static string FormatLapTime(float timeSeconds)
        {
            if (timeSeconds <= 0f) return "--:--.---";
            
            int minutes = (int)(timeSeconds / 60f);
            float remaining = timeSeconds - minutes * 60f;
            
            return $"{minutes}:{remaining:F3}";
        }
        
        /// <summary>
        /// Format tire temperature with color coding
        /// </summary>
        [BurstCompile]
        public static (string text, Color color) FormatTireTemp(float tempC)
        {
            Color color;
            
            if (tempC < 60f)
                color = new Color(0f, 0.5f, 1f, 1f); // Cold - blue
            else if (tempC < 80f)
                color = new Color(0f, 1f, 0f, 1f); // Optimal - green
            else if (tempC < 100f)
                color = new Color(1f, 1f, 0f, 1f); // Warm - yellow
            else
                color = new Color(1f, 0f, 0f, 1f); // Overheated - red
            
            return ($"{tempC:F0}°", color);
        }
        
        /// <summary>
        /// Get shift light color based on RPM
        /// </summary>
        [BurstCompile]
        public static Color GetShiftLightColor(float rpm, float redline, in VehicleConfig config)
        {
            float ratio = rpm / redline;
            
            if (ratio < 0.8f)
                return new Color(0f, 1f, 0f, 1f); // Green
            else if (ratio < 0.9f)
                return new Color(1f, 1f, 0f, 1f); // Yellow
            else if (ratio < 0.95f)
                return new Color(1f, 0.5f, 0f, 1f); // Orange
            else
                return new Color(1f, 0f, 0f, 1f); // Red
        }
    }
}
