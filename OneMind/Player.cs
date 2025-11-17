using System.Linq;
using System.Windows.Media.Media3D;
using Microsoft.Kinect;

namespace OneMind
{
    public class Player
    {
        public JointCollection Player1 { get; private set; }
        public JointCollection Player2 { get; private set; }

        public Vector3D[] Player1Vector { get; private set; }
        public Vector3D[] Player2Vector { get; private set; }

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

            MakePlayerVectorSet(Player1, Player1Vector);
            MakePlayerVectorSet(Player2, Player2Vector);
        }

        // 각 조인트를 다음 조인트와 비교하여 백터값 추출
        // 그렇게 생성된 백터 배열을 저장
        
        public void MakePlayerVectorSet(JointCollection player, Vector3D[] playerVector)
        {
            Joint hipcenter = player[JointType.HipCenter];
            Joint spine = player[JointType.Spine];
            Joint shouldercenter = player[JointType.ShoulderCenter];
            Joint head = player[JointType.Head];

            Joint shoulderleft = player[JointType.ShoulderLeft];
            Joint elbowleft = player[JointType.ElbowLeft];
            Joint wristleft = player[JointType.WristLeft];
            Joint handleft = player[JointType.HandLeft];

            Joint shoulderright = player[JointType.ShoulderRight];
            Joint elbowright = player[JointType.ElbowRight];
            Joint wristright = player[JointType.WristRight];
            Joint handright = player[JointType.HandRight];

            Joint hipleft = player[JointType.HipLeft];
            Joint kneeleft = player[JointType.KneeLeft];
            Joint ankleleft = player[JointType.AnkleLeft];
            Joint footleft = player[JointType.FootLeft];

            Joint hipright = player[JointType.HipRight];
            Joint kneeright = player[JointType.KneeRight];
            Joint ankleright = player[JointType.KneeRight];
            Joint footright = player[JointType.FootRight];

            playerVector.Append(GetVector(head, shouldercenter));
            playerVector.Append(GetVector(shouldercenter, spine));
            playerVector.Append(GetVector(spine, hipcenter));

            playerVector.Append(GetVector(shouldercenter, shoulderleft));
            playerVector.Append(GetVector(shoulderleft, elbowleft));
            playerVector.Append(GetVector(elbowleft, wristleft));
            playerVector.Append(GetVector(wristleft, handleft));

            playerVector.Append(GetVector(shouldercenter, shoulderright));
            playerVector.Append(GetVector(shoulderright, elbowright));
            playerVector.Append(GetVector(elbowright, wristright));
            playerVector.Append(GetVector(wristright, handright));

            playerVector.Append(GetVector(hipcenter, hipleft));
            playerVector.Append(GetVector(hipleft, kneeleft));
            playerVector.Append(GetVector(kneeleft, ankleleft));
            playerVector.Append(GetVector(ankleleft, footleft));

            playerVector.Append(GetVector(hipcenter, hipright));
            playerVector.Append(GetVector(hipright, kneeright));
            playerVector.Append(GetVector(kneeright, ankleright));
            playerVector.Append(GetVector(ankleright, footright));
        }

        public Vector3D GetVector(Joint head, Joint tail)
        {
            Vector3D vector = new Vector3D(head.Position.X - tail.Position.X,  head.Position.Y - tail.Position.Y, head.Position.Z - tail.Position.Z);
            return vector;
        }

    }
}
