using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Splines;

namespace Traversal
{
    [RequireComponent(typeof(SplineContainer))]
    public abstract class TraversalSplineBase : SplineComponent
    {
        //TODO: Support for closed splines
        // Note: currently, it could be redundant, but in the future there might be a need
        // to move across spline crossroads (a SplineContainer that has more than 1 spline), then, ActiveSpline would make sense.

        Spline _activeSpline;
        public Spline ActiveSpline 
        {
            get { return _activeSpline == null ? splineContainer.Splines[0] : _activeSpline; } 
            set { _activeSpline = value; } 
        }
        [SerializeField] protected SplineContainer splineContainer;

        #region Traversal Settings
        [Header("Traversal Settings")]
        [SerializeField] protected Vector3 Offset = Vector3.zero;
        [SerializeField, Tooltip("Speed in units per seconds")] protected float TraversalSpeed = 10f;
        [SerializeField, Tooltip("% of linear speed at given ")] protected float TraversalAngularSpeedModifier = 0.5f;
        [SerializeField, Tooltip("Speed in units per seconds")] protected float TraversalSpeedModifierMaxAngle = 30f;
        #endregion

        #region Orbital Movement Settings
        [Header("Orbital Movement Settings")]
        [SerializeField, Tooltip("Radius of the orbital movement around the spline.")]
        protected float orbitalRadius = 1f;
        [SerializeField, Tooltip("Angular speed of the orbital movement in degrees per second.")]
        protected float orbitalAngularSpeed = 30f;
        #endregion

        #region Aligment Settings
        /// <summary>
        /// Describes the ways the object can be aligned when animating along the spline.
        /// </summary>
        public enum AlignmentMode
        {
            /// <summary> No aligment is done and object's rotation is unaffected. </summary>
            [InspectorName("None")]
            None,
            /// <summary> The object's forward and up axes align to the spline's tangent and up vectors. </summary>
            [InspectorName("Spline Element")]
            SplineElement,
            /// <summary> The object's forward and up axes align to the spline tranform's z-axis and y-axis. </summary>
            [InspectorName("Spline Object")]
            SplineObject,
            /// <summary> The object's forward and up axes align to to the world's z-axis and y-axis. </summary>
            [InspectorName("World Space")]
            World
        }
        [Header("Traversal Alligment Settings")]
        [SerializeField, Tooltip("The coordinate space that the GameObject's up and forward axes align to.")]
        AlignmentMode m_AlignmentMode = AlignmentMode.SplineElement;
        [SerializeField, Tooltip("Which axis of the GameObject is treated as the forward axis.")]
        AlignAxis m_ObjectForwardAxis = AlignAxis.ZAxis;
        [SerializeField, Tooltip("Which axis of the GameObject is treated as the up axis.")]
        AlignAxis m_ObjectUpAxis = AlignAxis.YAxis;
        #endregion

        #region Other
        float sampleDelta = 0.01f;
        public float SampleDelta => sampleDelta;
        //Sample delta but in units size , used for some calculations
        float sampleDeltaLenght = 0;
        public float SampleDeltaLenght => sampleDeltaLenght;
        public float GetDeltaLenght(float t) => SplineWorldLenght * t;
        #endregion

        /// <summary>
        /// Position on a SPLINE in world space (this is not a Traversal Position - in that case use EvaluateTraversalPositionAndRotation)
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public Vector3 EvaluateWorldPosition(float t) => transform.TransformPoint(ActiveSpline.EvaluatePosition(t));

        /// <summary>
        /// Forward direction of the ActiveSpline at given point.
        /// </summary>
        /// <param name="traversalEval">In range of 0-1</param>
        /// <returns></returns>
        public Vector3 Forward(float traversalEval) => SplineUtility.EvaluateTangent(ActiveSpline, traversalEval);

        public Vector3 WorldForward(float traversalEval) => transform.TransformDirection(Forward(traversalEval).normalized);
        /// <summary>
        /// Up vector / Normal vector
        /// </summary>
        /// <param name="traversalEval"></param>
        /// <returns></returns>
        public Vector3 WorldUp(float traversalEval) => transform.TransformDirection(Up(traversalEval).normalized);
        public Vector3 WorldRight(float traversalEval) => transform.TransformDirection(Right(traversalEval).normalized);

        protected float SplineWorldLenght = 0;

        /// <summary>
        /// Up Vector of the ActiveSpline at given point.
        /// </summary>
        /// <param name="traversalEval">In range of 0-1</param>
        /// <returns></returns>
        public Vector3 Up(float traversalEval) => SplineUtility.EvaluateUpVector(ActiveSpline, traversalEval);
        /// <summary>
        /// Right Vector of the ActiveSpline at given point.
        /// </summary>
        /// <param name="traversalEval">In range of 0-1</param>
        /// <returns></returns>
        /// Binormal vector in Frenet-Serret frame
        public Vector3 Right(float traversalEval) => math.normalize(math.cross(Up(traversalEval), Forward(traversalEval)));

        public Vector3 Forward(float from, float to)
        {
            Vector3 fromPoint = SplineUtility.EvaluatePosition(ActiveSpline, from);
            Vector3 toPoint = SplineUtility.EvaluatePosition(ActiveSpline, to);
            Vector3 dir = (toPoint - fromPoint);
            return dir;
        }

        public Vector3 WorldForward(float from, float to)
        {
            Vector3 fromPoint = SplineUtility.EvaluatePosition(ActiveSpline, from);
            Vector3 toPoint = SplineUtility.EvaluatePosition(ActiveSpline, to);
            Vector3 dir = (toPoint - fromPoint).normalized;
            return transform.TransformDirection(dir);
        }

        public float GetWorldLength(int samples = 100)
        {
            float length = 0f;
            Vector3 prevPoint = transform.TransformPoint(ActiveSpline.EvaluatePosition(0f));
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                Vector3 currPoint = transform.TransformPoint(ActiveSpline.EvaluatePosition(t));
                length += Vector3.Distance(prevPoint, currPoint);
                prevPoint = currPoint;
            }
            return length;
        }

        protected void Awake()
        {
            TryGetComponent<SplineContainer>(out splineContainer);
            Assert.IsNotNull(splineContainer);
            SplineWorldLenght = GetWorldLength();
            sampleDeltaLenght = SplineWorldLenght * sampleDelta;
        }

        void AlignVectors(float t, out Vector3 forward, out Vector3 up)
        {
            forward = Vector3.forward;
            up = Vector3.up;

            switch (m_AlignmentMode)
            {
                case AlignmentMode.SplineElement:
                    forward = Forward(t);
                    if (Vector3.Magnitude(forward) <= Mathf.Epsilon)
                    {
                        if (t < 1f)
                            forward = Forward(Mathf.Min(1f, t + 0.01f));
                        else
                            forward = Forward(t - 0.01f);
                    }
                    forward.Normalize();
                    up = Up(t);
                    break;

                case AlignmentMode.SplineObject:
                    var objectRotation = transform.rotation;
                    forward = objectRotation * forward;
                    up = objectRotation * up;
                    break;

                default:
                    Debug.Log($"{m_AlignmentMode} alignment mode is not supported!", this);
                    break;
            }
        }

        /// <summary>
        /// Evaluates Traversal's position and rotation and applies orbital movement.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="orbitalAngle"></param>
        /// <param name="traversalPosition"></param>
        /// <param name="traversalRotation"></param>
        public void EvaluateTraversalPositionAndRotation<T>(T data, Vector3 offset, bool calculateOrbitalMovement = false) where T : TraversalSplineBaseData
        {
            data.SplineLocalPosition = data.TraversalPosition = ActiveSpline.EvaluatePosition(data.TraversalEval);
            data.SplineWorldPosition = transform.TransformPoint(data.TraversalPosition);
            
            data.TraversalRotation = Quaternion.identity;

            // Correct forward and up vectors based on axis remapping parameters
            var remappedForward = GetAxis(m_ObjectForwardAxis);
            var remappedUp = GetAxis(m_ObjectUpAxis);
            var axisRemapRotation = Quaternion.Inverse(Quaternion.LookRotation(remappedForward, remappedUp));

            if (m_AlignmentMode != AlignmentMode.None)
            {
                AlignVectors(data.TraversalEval, out var forward, out var up);
                data.TraversalRotation = Quaternion.LookRotation(forward, up) * axisRemapRotation;
                
                if(calculateOrbitalMovement)
                {
                    // Compute the orbital offset
                    // Rotate the up vector around the forward axis to get the orbital offset
                    Quaternion orbitalRotation = Quaternion.AngleAxis(data.TraversalOrbitalAngle, forward);

                    Vector3 orbitalOffset = orbitalRotation * (up * orbitalRadius);
                    data.TraversalPosition += orbitalOffset;
                }
                data.TraversalPosition += data.TraversalRotation * offset;
            }
            else
                data.TraversalRotation = transform.rotation;

            //Cast to world-space
            data.TraversalPosition = transform.TransformPoint(data.TraversalPosition);
        }

        public Spline GetClosestSpline(Vector3 worldPos, float minDistance = Mathf.Infinity)
        {
            Spline closestSpline = null;
            Vector3 localPos = transform.InverseTransformPoint(worldPos);

            foreach (Spline spline in splineContainer.Splines)
            {
                SplineUtility.GetNearestPoint(spline, localPos, out var nearest, out var t);
                float distance = Vector3.Distance(localPos, nearest);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestSpline = spline;
                }
            }
            if (closestSpline != null)
            {
                ActiveSpline = closestSpline;
            }
            else
            {
                Debug.Log("null spline!");
            }
            return closestSpline;
        }

        /// <summary>
        /// Gets the traversal position based on the worldPoint, useful for things like interacting with spline
        /// </summary>
        /// <param name="worldPoint"></param>
        /// <param name="t"></param>
        /// <param name="traversalPosition"></param>
        /// <param name="traversalRotation"></param>
        public void EvaluateTraversalAtPoint<T>(Vector3 worldPoint, T data, bool calculateOrbitalMovement = false) where T: TraversalSplineBaseData
        {
            var splineLocalPoint = transform.InverseTransformPoint(worldPoint);
            SplineUtility.GetNearestPoint(ActiveSpline, splineLocalPoint, out float3 nearest, out data.TraversalEval);
            data.TraversalEval =  PreciseEvalClamp01(data.TraversalEval);
            data.TraversalOrbitalAngle = CalculateOrbitalAngle(data.TraversalEval, worldPoint);
            EvaluateTraversalPositionAndRotation(data, Offset, calculateOrbitalMovement);
        }

        float evalPrecision = 0.001f;
        protected float PreciseEvalClamp01(float value)
        {
            float result = value;
            result = Mathf.Round(result / evalPrecision) * evalPrecision;
            if (Mathf.Abs(result) < evalPrecision)
            {
                result = 0f;
            }
            else if (Mathf.Abs(result - 1f) < evalPrecision)
            {
                result = 1f;
            }
            result = Mathf.Clamp01(result);
            return result;
        }

        /// <summary>
        /// Gets next traversal t based on the direction and speed.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="direction"></param>
        /// <param name="deltaTime"></param>
        /// <returns></returns>
        public float NextTraversalDelta(float t, float input, float deltaTime)
        {
            if ((t >= 1) && (input >= 1))
                return 0;
            else if ((t <= 0) && (input <= 0))
                return 0;
            float nextTsample = Mathf.Clamp01(t + (sampleDelta * input));
            return CalculateDeltaT(t, nextTsample, input, deltaTime);
        }

        public float NextTraversalOrbitalAngleDelta(float deltaTime, float input)
        {
            return (orbitalAngularSpeed * deltaTime * input);
        }

        float CalculateDeltaT(float from, float to, float input, float deltaTime)
        {
            Vector3 splineFromForward = Forward(from);
            splineFromForward.Normalize();
            Vector3 splineToForward = Forward(to);
            splineToForward.Normalize();
            float angleBetweenTangents = Vector3.Angle(splineFromForward, splineToForward);
            float normalizedAngle = Mathf.Clamp01(angleBetweenTangents / TraversalSpeedModifierMaxAngle);
            float speedModifier = Mathf.Lerp(1f, TraversalAngularSpeedModifier, normalizedAngle);
            float deltaT = (TraversalSpeed * speedModifier * deltaTime * input) / SplineWorldLenght;
            return deltaT;
        }

        public float CalculateOrbitalAngle(float t, Vector3 playerPosition)
        {
            // Get the spline's world position and orientation at parameter 't'
            Vector3 splineLocalPosition = ActiveSpline.EvaluatePosition(t);
            Vector3 splineWorldPosition = transform.TransformPoint(splineLocalPosition);

            Vector3 forwardLocal;
            Vector3 upLocal = Up(t);

            if (Mathf.Approximately(t, 0f))
            {
                forwardLocal = Forward(0, SampleDelta);
            }
            else if (Mathf.Approximately(t, 1f))
            {
                forwardLocal = Forward(1 - SampleDelta, 1);
            }
            else
            {
                forwardLocal = Forward(t);
            }


            Vector3 forward = transform.TransformDirection(forwardLocal).normalized;
            Vector3 up = transform.TransformDirection(upLocal).normalized;

            //  Calculate the vector from the spline to the player
            Vector3 toPlayer = playerPosition - splineWorldPosition;

            // Project 'toPlayer' onto the plane perpendicular to 'forward'
            Vector3 projectedToPlayer = Vector3.ProjectOnPlane(toPlayer, forward);

            //  Calculate the angle between 'up' and 'projectedToPlayer' around 'forward'
            float angle = Vector3.SignedAngle(up, projectedToPlayer, forward);
            // Clamp the angle is in the range [0, 360)
            if (angle < 0)
            {
                angle += 360f;
            }

            return angle;
        }

        //TODO: Finish implementing logics of switching active spline, this method will be useful later.
        //Switching active spline is necessary for implementing crossed splines - when two splines connects to eachother "Crossroads"
        bool ClosestKnot(Spline spline, Vector3 localPosition, out BezierKnot closestKnot, float minDistance = 0.2f)
        {
            closestKnot = default;
            bool knotFound = false;
            foreach (var knot in spline.Knots)
            {
                float distance = Vector3.Distance(knot.Position, localPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestKnot = knot;
                    knotFound = true;
                }
            }
            return knotFound;
        }
        
    }
}
