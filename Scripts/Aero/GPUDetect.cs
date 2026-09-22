using UnityEngine;

namespace RacingSim.Aero
{
    /// <summary>
    /// GPU detection and tier classification for adaptive aero quality.
    /// Determines which aero simulation level to use based on hardware.
    /// </summary>
    public static class GPUDetect
    {
        public struct GPUDetectResult
        {
            public string Name;
            public string Vendor;
            public int VRAM_MB;
            public int Tier;
            public int Score;
            public bool SupportsCompute;
            public int MaxTextureSize;
        }

        public static GPUDetectResult DetectAndClassify()
        {
            GPUDetectResult result = new GPUDetectResult();

            result.Name = SystemInfo.graphicsDeviceName;
            result.Vendor = SystemInfo.graphicsDeviceVendor;
            result.VRAM_MB = SystemInfo.graphicsMemorySize;
            result.SupportsCompute = SystemInfo.supportsComputeShaders;
            result.MaxTextureSize = SystemInfo.maxTextureSize;

            int score = 0;

            if (result.VRAM_MB >= 16384) score += 50;
            else if (result.VRAM_MB >= 12288) score += 40;
            else if (result.VRAM_MB >= 8192) score += 30;
            else if (result.VRAM_MB >= 4096) score += 20;
            else if (result.VRAM_MB >= 2048) score += 10;
            else score += 5;

            string nameLower = result.Name.ToLower();

            if (nameLower.Contains("rtx 40") || nameLower.Contains("rx 7900")
                || nameLower.Contains("rx 7800") || nameLower.Contains("rx 7600"))
            {
                score += 50;
            }
            else if (nameLower.Contains("rtx 30") || nameLower.Contains("rx 6900")
                     || nameLower.Contains("rx 6800") || nameLower.Contains("rx 6700"))
            {
                score += 40;
            }
            else if (nameLower.Contains("rtx 20") || nameLower.Contains("rx 5700")
                     || nameLower.Contains("rx 5600"))
            {
                score += 30;
            }
            else if (nameLower.Contains("gtx 16") || nameLower.Contains("gtx 10")
                     || nameLower.Contains("rx 590") || nameLower.Contains("rx 580"))
            {
                score += 20;
            }
            else if (nameLower.Contains("gtx 9") || nameLower.Contains("gtx 7")
                     || nameLower.Contains("rx 480") || nameLower.Contains("rx 470"))
            {
                score += 10;
            }
            else
            {
                score += 5;
            }

            if (result.SupportsCompute) score += 10;
            if (SystemInfo.supportedRenderTargetCount >= 8) score += 5;
            if (result.MaxTextureSize >= 16384) score += 5;

            if (score >= 100) result.Tier = 4;
            else if (score >= 80) result.Tier = 3;
            else if (score >= 60) result.Tier = 2;
            else if (score >= 35) result.Tier = 1;
            else result.Tier = 0;

            result.Score = score;
            return result;
        }
    }
}
