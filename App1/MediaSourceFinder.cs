using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Capture.Frames;

namespace App1
{
    static class MediaSourceFinder
    {
        public static async Task<MediaFrameSourceGroup> FindGroupsWithAllSourceKindsAsync(
            params MediaFrameSourceKind[] sourceKinds)
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();

            var firstGroupWithAllSourceKinds =
                groups.FirstOrDefault(
                    g => sourceKinds.All(k => g.SourceInfos.Any(si => si.SourceKind == k)));

            return (firstGroupWithAllSourceKinds);
        }
    }
}