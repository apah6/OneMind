using System.Linq;
using Microsoft.Kinect;

namespace OneMind
{
    public class Player
    {
        public JointCollection Player1 { get; private set; }
        public JointCollection Player2 { get; private set; }

        public Player()
        {
            Player1 = null;
            Player2 = null;
        }

        // Recognize에서 프레임이 올 때마다 호출해서 최신 JointCollection을 반영
        public void Update(Skeleton[] skeletons)
        {
            if (skeletons == null)
            {
                Player1 = Player2 = null;
                return;
            }

            var tracked = skeletons
                .Where(s => s != null && s.TrackingState == SkeletonTrackingState.Tracked)
                .Take(2)
                .ToArray();

            Player1 = tracked.Length > 0 ? tracked[0].Joints : null;
            Player2 = tracked.Length > 1 ? tracked[1].Joints : null;
        }
    }
}
