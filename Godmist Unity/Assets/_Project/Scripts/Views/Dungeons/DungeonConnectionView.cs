using UnityEngine;

namespace Godmist.Views.Dungeons
{
    public class DungeonConnectionView : MonoBehaviour
    {
        [SerializeField] private Transform segmentA;
        [SerializeField] private Transform segmentB;
        [SerializeField] private float thickness = 0.3f;

        public void Bind(
            Bounds firstRoomBounds,
            Bounds secondRoomBounds,
            Vector3 bendPosition)
        {
            if (TryGetStraightConnection(
                firstRoomBounds,
                secondRoomBounds,
                out var startPosition,
                out var endPosition))
            {
                SetSegment(
                    segmentA,
                    startPosition,
                    endPosition);

                SetSegmentActive(segmentB, false);
                return;
            }

            var firstEdgePosition = GetRoomEdgePosition(
                firstRoomBounds,
                bendPosition);

            var secondEdgePosition = GetRoomEdgePosition(
                secondRoomBounds,
                bendPosition);

            SetSegment(
                segmentA,
                firstEdgePosition,
                bendPosition);

            SetSegment(
                segmentB,
                bendPosition,
                secondEdgePosition);
        }

        private static bool TryGetStraightConnection(
            Bounds firstRoomBounds,
            Bounds secondRoomBounds,
            out Vector3 startPosition,
            out Vector3 endPosition)
        {
            if (TryGetOverlappingRange(
                firstRoomBounds.min.x,
                firstRoomBounds.max.x,
                secondRoomBounds.min.x,
                secondRoomBounds.max.x,
                out var minimumX,
                out var maximumX))
            {
                var sharedX = (minimumX + maximumX) * 0.5f;

                if (firstRoomBounds.center.y
                    < secondRoomBounds.center.y)
                {
                    startPosition = new Vector3(
                        sharedX,
                        firstRoomBounds.max.y,
                        0f);

                    endPosition = new Vector3(
                        sharedX,
                        secondRoomBounds.min.y,
                        0f);
                }
                else
                {
                    startPosition = new Vector3(
                        sharedX,
                        firstRoomBounds.min.y,
                        0f);

                    endPosition = new Vector3(
                        sharedX,
                        secondRoomBounds.max.y,
                        0f);
                }

                return true;
            }

            if (TryGetOverlappingRange(
                firstRoomBounds.min.y,
                firstRoomBounds.max.y,
                secondRoomBounds.min.y,
                secondRoomBounds.max.y,
                out var minimumY,
                out var maximumY))
            {
                var sharedY = (minimumY + maximumY) * 0.5f;

                if (firstRoomBounds.center.x
                    < secondRoomBounds.center.x)
                {
                    startPosition = new Vector3(
                        firstRoomBounds.max.x,
                        sharedY,
                        0f);

                    endPosition = new Vector3(
                        secondRoomBounds.min.x,
                        sharedY,
                        0f);
                }
                else
                {
                    startPosition = new Vector3(
                        firstRoomBounds.min.x,
                        sharedY,
                        0f);

                    endPosition = new Vector3(
                        secondRoomBounds.max.x,
                        sharedY,
                        0f);
                }

                return true;
            }

            startPosition = default;
            endPosition = default;
            return false;
        }

        private static bool TryGetOverlappingRange(
            float firstMinimum,
            float firstMaximum,
            float secondMinimum,
            float secondMaximum,
            out float overlapMinimum,
            out float overlapMaximum)
        {
            overlapMinimum = Mathf.Max(
                firstMinimum,
                secondMinimum);

            overlapMaximum = Mathf.Min(
                firstMaximum,
                secondMaximum);

            return overlapMinimum < overlapMaximum;
        }

        private static Vector3 GetRoomEdgePosition(
            Bounds roomBounds,
            Vector3 targetPosition)
        {
            var center = roomBounds.center;
            var direction = targetPosition - center;

            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return center + new Vector3(
                    Mathf.Sign(direction.x) * roomBounds.extents.x,
                    0f,
                    0f);
            }

            return center + new Vector3(
                0f,
                Mathf.Sign(direction.y) * roomBounds.extents.y,
                0f);
        }

        private void SetSegment(
            Transform segment,
            Vector3 startPosition,
            Vector3 endPosition)
        {
            if (segment == null) return;

            var direction = endPosition - startPosition;

            if (direction.sqrMagnitude < 0.0001f)
            {
                SetSegmentActive(segment, false);
                return;
            }

            SetSegmentActive(segment, true);

            segment.position = (startPosition + endPosition) * 0.5f;

            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                segment.localScale = new Vector3(
                    Mathf.Abs(direction.x),
                    thickness,
                    1f);
            }
            else
            {
                segment.localScale = new Vector3(
                    thickness,
                    Mathf.Abs(direction.y),
                    1f);
            }
        }

        private static void SetSegmentActive(
            Transform segment,
            bool isActive)
        {
            if (segment != null)
            {
                segment.gameObject.SetActive(isActive);
            }
        }
    }
}