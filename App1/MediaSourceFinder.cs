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
        public static List<string> FindSourceInfosWithMaxFrameRates(
            MediaFrameSourceGroup sourceGroup, params MediaFrameSourceKind[] sourceKinds)
        {
            var listSourceInfos = new List<string>();

            foreach (var kind in sourceKinds)
            {
                var sourceInfos =
                    sourceGroup.SourceInfos.Where(s => s.SourceKind == kind);

                var maxInfo = sourceInfos.OrderByDescending(
                    si => si.VideoProfileMediaDescription.Max(
                        msd => msd.FrameRate * msd.Height * msd.Width)).First();

                listSourceInfos.Add(maxInfo.Id);
            }
            return (listSourceInfos);
        }
    }
}