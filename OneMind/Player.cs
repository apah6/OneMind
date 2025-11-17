using System.Linq;
using System.Windows.Media.Media3D;
using Microsoft.Kinect;

namespace OneMind
{
    public class Player
    {
        public JointCollection Player1 { get; private set; }
        public JointCollection Player2 { get; private set; }

        // Recognize가 기대하는 이름/타입과 일치시킴
        public Vector3D[] Player1Vector { get; private set; }
        public Vector3D[] Player2Vector { get; private set; }

        // segments 길이와 동일한 벡터 배열을 항상 유지
        private static readonly (JointType a, JointType b)[] segments = new (JointType, JointType)[]
        {
            (JointType.HipCenter, JointType.Spine),
            (JointType.Spine, JointType.ShoulderCenter),
            (JointType.ShoulderCenter, JointType.Head),
            (JointType.ShoulderCenter, JointType.ShoulderLeft),
            (JointType.ShoulderLeft, JointType.ElbowLeft),
            (JointType.ElbowLeft, JointType.WristLeft),
            (JointType.WristLeft, JointType.HandLeft),
            (JointType.ShoulderCenter, JointType.ShoulderRight),
            (JointType.ShoulderRight, JointType.ElbowRight),
            (JointType.ElbowRight, JointType.WristRight),
            (JointType.WristRight, JointType.HandRight),
            (JointType.HipCenter, JointType.HipLeft),
            (JointType.HipLeft, JointType.KneeLeft),
            (JointType.KneeLeft, JointType.AnkleLeft),
            (JointType.AnkleLeft, JointType.FootLeft),
            (JointType.HipCenter, JointType.HipRight),
            (JointType.HipRight, JointType.KneeRight),
            (JointType.KneeRight, JointType.AnkleRight),
            (JointType.AnkleRight, JointType.FootRight)
        };

        public Player()
        {
            Player1 = null;
            Player2 = null;
            Player1Vector = new Vector3D[segments.Length];
            Player2Vector = new Vector3D[segments.Length];

            // 초기값은 모두 길이 0으로 표시 (invalid)
            for (int i = 0; i < segments.Length; i++)
            {
                Player1Vector[i] = new Vector3D(0, 0, 0);
                Player2Vector[i] = new Vector3D(0, 0, 0);
            }
        }

        // Recognize에서 프레임이 올 때마다 호출해서 최신 JointCollection을 반영
        public void Update(Skeleton[] skeletons)
        {
            if (skeletons == null || skeletons.Length == 0)
            {
                Player1 = Player2 = null;
                // 벡터는 0으로 유지(비활성 표시)
                for (int i = 0; i < segments.Length; i++)
                {
                    Player1Vector[i] = new Vector3D(0, 0, 0);
                    Player2Vector[i] = new Vector3D(0, 0, 0);
                }
                return;
            }

            var tracked = skeletons
                .Where(s => s != null && s.TrackingState == SkeletonTrackingState.Tracked)
                .Take(2)
                .ToArray();

            Player1 = tracked.Length > 0 ? tracked[0].Joints : null;
            Player2 = tracked.Length > 1 ? tracked[1].Joints : null;

            // 벡터 계산 (존재하지 않으면 0벡터 유지)
            if (Player1 != null)
                FillVectorsFromJoints(Player1, Player1Vector);
            else
                for (int i = 0; i < segments.Length; i++) Player1Vector[i] = new Vector3D(0, 0, 0);

            if (Player2 != null)
                FillVectorsFromJoints(Player2, Player2Vector);
            else
                for (int i = 0; i < segments.Length; i++) Player2Vector[i] = new Vector3D(0, 0, 0);
        }

        private void FillVectorsFromJoints(JointCollection player, Vector3D[] target)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                var pa = GetJointPositionIfAvailable(player, seg.a);
                var pb = GetJointPositionIfAvailable(player, seg.b);

                if (!pa.HasValue || !pb.HasValue)
                {
                    // 불완전하면 0벡터로 표시 (Recognize에서 유효성 검사 시 무시됨)
                    target[i] = new Vector3D(0, 0, 0);
                }
                else
                {
                    target[i] = new Vector3D(
                        pb.Value.X - pa.Value.X,
                        pb.Value.Y - pa.Value.Y,
                        pb.Value.Z - pa.Value.Z);
                }
            }
        }

        private static SkeletonPoint? GetJointPositionIfAvailable(JointCollection jc, JointType jt)
        {
            if (jc == null) return null;
            Joint j = jc[jt];
            if (j == null) return null;
            if (j.TrackingState == 0) return null;
            return j.Position;
        }
    }
}
