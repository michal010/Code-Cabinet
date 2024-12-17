using Traversal.InteractionScenarios;
using Traversal.Transitions;
using Traversal.Transitions.Factory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;

namespace Traversal
{
    public class KCC_TraversalSplineData : TraversalSplineBaseData
    {
        public PlayerCharacterInputs PlayerInput;
        public Vector3 moveInputVector;
        public Vector3 cameraPlanarDirection;
        public Quaternion cameraPlanarRotation;
        public Vector3 worldPlanarInputVector;

        public KCC_TraversalSplineData() : base()
        {
        }

        public override void CopyFrom(TraversalSplineBaseData data)
        {
            base.CopyFrom(data);
            if(data is KCC_TraversalSplineData kccSplineData)
            {
                PlayerInput = kccSplineData.PlayerInput;
                moveInputVector = kccSplineData.moveInputVector;
                cameraPlanarDirection = kccSplineData.cameraPlanarDirection;
                cameraPlanarRotation = kccSplineData.cameraPlanarRotation;
                worldPlanarInputVector = kccSplineData.worldPlanarInputVector;
            }
        }

        public override string ToString()
        {
            return $"Traversal Eval: {TraversalEval} \n TraversalOrbitalAngle: {TraversalOrbitalAngle} \n TraversalRotation: {TraversalRotation} \n TraversalPosition: {TraversalPosition} \n TraversalInput: {TraversalInput}";
        }
    }

    //[CreateAssetMenu("")]
    public class KCC_TraversalSplineAnimationDataSO : ScriptableObject
    {
        AnimationClip OnTraversalEnterClip { get; set; }
        AnimationClip OnTraversalExitClip { get; set; }
        AnimationClip OnTraversalClip { get; set; }
    }

    public class TraversalSplineBaseData
    {
        public float TraversalEval;
        public float TraversalOrbitalAngle;
        public Quaternion TraversalRotation;
        public Vector3 TraversalPosition;
        public Vector3 TraversalInput;
        public Vector3 SplineWorldPosition;
        public Vector3 SplineLocalPosition;

        public TraversalSplineBaseData()
        {
            TraversalEval = 0f;
            TraversalOrbitalAngle = 0f;
            TraversalPosition = Vector3.zero;
            TraversalRotation = Quaternion.identity;
            TraversalInput = Vector3.zero;
        }

        public virtual void CopyFrom(TraversalSplineBaseData data)
        {
            TraversalEval = data.TraversalEval;
            TraversalOrbitalAngle = data.TraversalOrbitalAngle;
            TraversalRotation = data.TraversalRotation;
            TraversalPosition = data.TraversalPosition;
            TraversalInput = data.TraversalInput;
            SplineWorldPosition = data.SplineWorldPosition;
            SplineLocalPosition = data.SplineLocalPosition;
        }
    }

    [Serializable]
    public struct KCC_TraversalSpline_Connection
    {
        public KCC_TraversalSpline To;
        [Range(0,1)] public float ToPosition;
        [Range(0,1)] public float FromPosition;
    }

    public interface IKCC_TraversalAnimationProvider
    {
        AnimationClip OnTraversalEnterClip { get; set; }
        AnimationClip OnTraversalExitClip { get; set; }
        AnimationClip OnTraversalClip { get; set; }
    }

    //TODO: instead of implementing new methods to update the Motor, just use ICharacterControler from KCC like character movement states do.
    public interface IKCC_TraversalSpline
    {
        public KCC_TraversalSplineData CreateTraversalData();
        //public void UpdateKCC(ref Vector3 currentVelocity, KCC_Context Context, KCC_TraversalSplineData data, float deltaTime);
        public void UpdatePosition(ref Vector3 currentVelocity, KCC_Context Context, KCC_TraversalSplineData data, float deltaTime);
        public void UpdateRotation(ref Quaternion currentRotation, KCC_Context Context, KCC_TraversalSplineData data, float deltaTime);
        public void MapTraversalInput(KCC_TraversalSplineData data, PlayerCharacterInputs inputs, Vector3 moveInputVector, Vector3 cameraPlanarDirection, Quaternion cameraPlanarRotation, Vector3 worldPlanarInputVector);
        public void OnTraversalEnter(KCC_Context Context, KCC_TraversalSplineData data);
        public void OnTraversalExit(KCC_Context Context);
        public bool ClosestConnection(float fromPointEval, out KCC_TraversalSpline traversalSpline, out float connectionPointEval);
        public void EvaluateNextTraversalProgress(KCC_Context Context, KCC_TraversalSplineData data, float deltaTime);
        public void AtTraversalZeroReached(KCC_Context Context, KCC_TraversalSplineData data);
        public void AtTraversalOneReached(KCC_Context Context, KCC_TraversalSplineData data);
        public bool CanEnterFromLocomotionState(KCC_Context Context, PlayerCharacterInputs inputs, KCC_TraversalSplineData data, bool byCollision = false);
        //public bool CanEnterFromLocomotionGroundCollision();
        //public bool CanEnterFromLocomotionInMidAirCollision();
        //public bool CanEnterFromLocomotionOnGroundByLookAtInteraction();
        //public bool CanEnterFromLocomotionInMidAirByLookAtInteraction();
        public bool HasTraversalInteraction(KCC_Context Context, KCC_TraversalSplineData fromData, out TraversalToTraversalInteractionData result);
        public void Jump(ref Vector3 currentVelocity, KCC_Context Context, float deltaTime);
        public IEnumerator AnchorPlayer(KCC_Context Context, KCC_TraversalSplineData data);
    }

    public class TraversalToTraversalInteractionData
    {
        public IKCC_TraversalSpline fromTraversal;
        public IKCC_TraversalSpline toTraversal;
        public KCC_TraversalSplineData fromData;
        public KCC_TraversalSplineData toData;
        public ITraversalTransitionStrategy traversalTransitionStrategy;

        public TraversalToTraversalInteractionData(IKCC_TraversalSpline fromTraversal, IKCC_TraversalSpline toTraversal, KCC_TraversalSplineData fromData, KCC_TraversalSplineData toData, ITraversalTransitionStrategy traversalTransitionStrategy)
        {
            this.fromTraversal = fromTraversal;
            this.toTraversal = toTraversal;
            this.fromData = fromData;
            this.toData = toData;
            this.traversalTransitionStrategy = traversalTransitionStrategy;
        }
    }

    public class KCC_TraversalSpline : TraversalSplineBase, IKCC_TraversalSpline
    {
        [Header("Traversal Interaction Scenarios")]
        public List<TraversalEntranceScenarioSO> LocomotionDistanceInteractionScenarios;
        public List<TraversalEntranceScenarioSO> LocomotionCollisionScenarios;

        public bool CheckForCollisions = true;
        [Header("Orbital Settings")]
        public bool AllowOrbitalMovement = false;
        [Header("Connections Settings")]
        [SerializeField] List<KCC_TraversalSpline_Connection> Connections = new List<KCC_TraversalSpline_Connection>();
        /// <summary>
        /// How close player has to be to the connection point to be considered as on the point
        /// </summary>
        [SerializeField, Tooltip("The distance from the player to the connection point for which the player will be considered as connection point.")]
        float ConnectionCaptureDistance = 2f;

        [Header("Traversal-to-traversal Settings")]
        [field: SerializeField] bool AllowInteractionWithOtherTraversals = true;
        [field: SerializeField] float OtherTraversalInteractionRange = 5f;


        // Could be possibly moved to separate utils class / extension class
        #region HelperMethods
        public Vector3 DirectionFromSplinePosToPlayerFeet(KCC_Context Context, KCC_TraversalSplineData data)
        {
            return (Context.Motor.TransientPosition - data.SplineWorldPosition).normalized;
        }
        public Vector3 DirectionPlayerFeetToSplinePos(KCC_Context Context, KCC_TraversalSplineData data)
        {
            return (data.SplineWorldPosition - Context.Motor.TransientPosition).normalized;
        }
        public Vector3 DirectionPlayerCameraToSplinePos(KCC_Context Context, KCC_TraversalSplineData data)
        {
            return (data.SplineWorldPosition - Context.CameraTransform.position).normalized;
        }
        //Dir vector -> targetPos - fromPos
        public Vector3 DirectionPlayerMidPointToSplinePos(KCC_Context Context, KCC_TraversalSplineData data)
        {
            Vector3 midPoint = Context.Motor.TransientPosition + (Context.Controller.LocomotionState.DefaultColliderHeight / 2) * Context.Motor.CharacterUp;
            return (data.SplineWorldPosition - midPoint).normalized;
        }
        #endregion

        #region Scenario Methods

        public bool TryEvaluateDistanceInteractionScenarios(KCC_Context context, KCC_TraversalSplineData data, PlayerCharacterInputs inputs, out TraversalEntranceScenarioSO scenario)
        {
            foreach (var entranceScenario in LocomotionDistanceInteractionScenarios)
            {
                if (entranceScenario.Evaluate(context, this, data, inputs, out string reason))
                {
                    Debug.Log($"Scenario '{entranceScenario.ScenarioName}' passed.");
                    scenario = entranceScenario;
                    return true;
                }
            }

            scenario = null;
            return false;
        }

        public bool TryEvaluateCollisionInteractionScenarios(KCC_Context context, KCC_TraversalSplineData data, PlayerCharacterInputs inputs, out TraversalEntranceScenarioSO scenario)
        {
            foreach (var entranceScenario in LocomotionCollisionScenarios)
            {
                if (entranceScenario.Evaluate(context, this, data, inputs, out string reason))
                {
                    Debug.Log($"Scenario '{entranceScenario.ScenarioName}' passed.");
                    scenario = entranceScenario;
                    return true;
                }
            }

            scenario = null;
            return false;
        }

        #endregion

        public virtual void AtTraversalZeroReached(KCC_Context Context, KCC_TraversalSplineData data) { }
        public virtual void AtTraversalOneReached(KCC_Context Context, KCC_TraversalSplineData data) { }

        protected void OnDrawGizmosSelected()
        {
            foreach (var c in Connections)
            {
                if (c.To == null)
                    return;
                var fromPoint = EvaluateWorldPosition(c.FromPosition);
                var toPoint = c.To.EvaluateWorldPosition(c.ToPosition);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(fromPoint, .25f);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(fromPoint, toPoint);
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(toPoint, .25f);
            }
            Vector3 pos = EvaluateWorldPosition(0.5f);
            var data = CreateTraversalData();
            data.TraversalEval = 0.5f;
            EvaluateTraversalPositionAndRotation(data, Offset);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(data.TraversalPosition, 0.5f);

            // Forward (Niebieski wektor)
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pos, pos + WorldForward(0.5f) * 2);

            // Up (Zielony wektor)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pos, pos + WorldUp(0.5f) * 2);

            // Right (Czerwony wektor)
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, pos + WorldRight(0.5f) * 2);
        }

        private void OnValidate()
        {
            // Pobranie komponentu tylko w edytorze
            if (splineContainer == null)
            {
                splineContainer = GetComponent<SplineContainer>();
            }
        }

        public bool ClosestConnection(float fromPointEval, out KCC_TraversalSpline traversalSpline, out float connectionPointEval)
        {
            float distEval = 0;
            float dist = 0;
            float minDistance = Mathf.Infinity;
            traversalSpline = null;
            connectionPointEval = default;
            foreach (var c in Connections)
            {
                distEval = Mathf.Abs(c.FromPosition - fromPointEval);
                dist = GetDeltaLenght(distEval);
                if(dist <= ConnectionCaptureDistance)
                {
                    //Given point (fromPointEval) is close enough to connection point
                    if(dist < minDistance)
                    {
                        minDistance = dist;
                        traversalSpline = c.To;
                        connectionPointEval = c.ToPosition;
                    }
                }
            }
            return traversalSpline != null ? true : false;
        }

        public virtual void OnTraversalEnter(KCC_Context Context, KCC_TraversalSplineData data)
        {
            GetClosestSpline(Context.Motor.TransientPosition);
            EvaluateTraversalAtPoint(Context.Motor.TransientPosition, data);
        }

        public virtual void OnTraversalExit(KCC_Context Context)
        {
        }

        float verticalThreshold = 0.7f;

        public virtual void MapTraversalInput(KCC_TraversalSplineData data, PlayerCharacterInputs inputs, Vector3 moveInputVector, Vector3 cameraPlanarDirection, Quaternion cameraPlanarRotation, Vector3 worldPlanarInputVector)
        {
            Vector3 mappedInput = Vector3.zero;
            data.PlayerInput = inputs;
            data.moveInputVector = moveInputVector;
            data.cameraPlanarDirection = cameraPlanarDirection;
            data.cameraPlanarRotation = cameraPlanarRotation; 
            data.worldPlanarInputVector = worldPlanarInputVector;

            Vector3 forward;

            // Handle edge cases at the start and end of the spline
            if (Mathf.Approximately(data.TraversalEval, 0f))
            {
                forward = WorldForward(0, SampleDelta);
            }
            else if (Mathf.Approximately(data.TraversalEval, 1f))
            {
                forward = WorldForward(1 - SampleDelta, 1);
            }
            else
            {
                forward = WorldForward(data.TraversalEval);
            }

            // Calculate alignment for forward/backward movement
            float inputAlignment;
            float verticality = Mathf.Abs(forward.y);

            if (verticality > verticalThreshold)
            {
               // inputAlignment = moveInputVector.z * Mathf.Sign(splineWorldForward.y);
                inputAlignment = moveInputVector.z * Mathf.Sign(forward.y);
            }
            else
            {
               // inputAlignment = Vector3.Dot(worldPlanarInputVector, splineWorldForward);
                inputAlignment = Vector3.Dot(worldPlanarInputVector, forward);
            }

            if (inputAlignment > 0.7)
                mappedInput.z = 1;
            else if (inputAlignment < -0.7)
                mappedInput.z = -1;

            if(AllowOrbitalMovement)
            {
                if (moveInputVector.x == 0)
                    mappedInput.x = 0;
                else
                    mappedInput.x = -Mathf.Sign(moveInputVector.x);
            }
            data.TraversalInput = mappedInput;
        }
        
        public virtual void EvaluateNextTraversalProgress(KCC_Context Context, KCC_TraversalSplineData data, float deltaTime)
        {
            //Copy the data so we can check if player movement is valid
            KCC_TraversalSplineData tmpData = new KCC_TraversalSplineData();
            tmpData.CopyFrom(data);

            if(AllowOrbitalMovement)
            {
                //data.TraversalOrbitalAngle += NextTraversalOrbitalAngleDelta(deltaTime, data.TraversalInput.x);
                tmpData.TraversalOrbitalAngle += NextTraversalOrbitalAngleDelta(deltaTime, tmpData.TraversalInput.x);
            }

            //data.TraversalEval += NextTraversalDelta(data.TraversalEval, data.TraversalInput.z, deltaTime);
            tmpData.TraversalEval += NextTraversalDelta(tmpData.TraversalEval, tmpData.TraversalInput.z, deltaTime);
            if (ActiveSpline.Closed)
            {
                //data.TraversalEval = (data.TraversalEval % 1f + 1f) % 1f;
                tmpData.TraversalEval = (tmpData.TraversalEval % 1f + 1f) % 1f;
            }

            //data.TraversalEval = PreciseEvalClamp01(data.TraversalEval);
            tmpData.TraversalEval = PreciseEvalClamp01(tmpData.TraversalEval);

            if(CheckForCollisions)
            {
                EvaluateTraversalPositionAndRotation(tmpData, Offset, AllowOrbitalMovement);
                if (!MotorCollidesAtPosition(Context, tmpData))
                {
                    // Apply changes if position is valid
                    data.CopyFrom(tmpData);
                }
                else
                    return;
            }
            else
            {
                // Apply changes regardless of collision
                data.CopyFrom(tmpData);
            }
            
        }

        //TODO: Traversal input is passed here for ease of implementing diffrent behaviours on traversals
        //however it seems odd to pass traversalInput along traversalEval and traversalOrbitalAngle, since they are being calculated soly
        //on traversalInput, so it might be obselote in the future.
        /// <summary>
        /// Updates motor's position.
        /// </summary>
        /// <param name="Motor"></param>
        /// <param name="traversalEval"></param>
        /// <param name="traversalOrbitalAngle"></param>

        // Collision guard check
        public bool MotorCollidesAtPosition(KCC_Context Context, KCC_TraversalSplineData data)
        {
            Collider[] cols = new Collider[2];
            int hitCount = Context.Motor.CharacterCollisionsOverlap(data.TraversalPosition, Context.Motor.TransientRotation, cols);

            bool hasCollisions = cols
                .Where(c => c != null && c.gameObject != this.gameObject)
                .Any();

            return hasCollisions;
        }

        public virtual void UpdatePosition(ref Vector3 currentVelocity, KCC_Context Context, KCC_TraversalSplineData data, float deltaTime)
        {
            EvaluateTraversalPositionAndRotation(data, Offset, AllowOrbitalMovement);
            Context.Motor.SetTransientPosition(data.TraversalPosition);
        }

        public virtual void UpdateRotation(ref Quaternion currentRotation, KCC_Context Context, KCC_TraversalSplineData data, float deltaTime) 
        {
            var MoveInputVector = data.cameraPlanarRotation * data.moveInputVector;
            var LookInputVector = data.cameraPlanarDirection;
            if (LookInputVector != Vector3.zero && Context.Controller.OrientationSharpness > 0.0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Context.Motor.CharacterForward, LookInputVector,
                    1 - Mathf.Exp(-Context.Controller.OrientationSharpness * deltaTime)).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Context.Motor.CharacterUp);
            }

            if (Context.Controller.OrientTowardsGravity)
            {
                // Rotate from current up to invert gravity
                currentRotation = Quaternion.FromToRotation((currentRotation * Vector3.up), -Context.Controller.Gravity) *
                                  currentRotation;
            }
        }
        
        public void EvaluateTraversalAtPoint<T>(Vector3 worldPoint, T data) where T : KCC_TraversalSplineData
        {
            EvaluateTraversalAtPoint(worldPoint, data, AllowOrbitalMovement);
        }

        public virtual KCC_TraversalSplineData CreateTraversalData()
        {
            return new KCC_TraversalSplineData();
        }

        public virtual bool CanEnterFromLocomotionState(KCC_Context Context, PlayerCharacterInputs inputs, KCC_TraversalSplineData data, bool byCollision = false)
        {
            //On ground interactions
            if(Context.Motor.GroundingStatus.IsStableOnGround)
            {
                if (CheckForCollisions)
                {
                    KCC_TraversalSplineData tmpData = CreateTraversalData();
                    EvaluateTraversalAtPoint(Context.Motor.TransientPosition, tmpData);
                    if (!MotorCollidesAtPosition(Context, tmpData))
                    {
                        return inputs.JumpPerformed;
                    }
                    else
                        return false;
                }
            }
            else // in air
            {
                return true;
            }

            return false;



            //GetClosestSpline(Context.Motor.TransientPosition);
            //var data = CreateTraversalData();
            //EvaluateTraversalAtPoint(Context.Motor.TransientPosition, data);
            ////calc dir to player cam
            //Vector3 dirToPlayer = (Context.CameraTransform.position - data.TraversalPosition).normalized;
            //Vector3 planarDirToPlayer = Vector3.ProjectOnPlane(dirToPlayer, Vector3.up).normalized;
            //Vector3 characterForwardDir = Vector3.ProjectOnPlane(Context.Motor.CharacterForward, Vector3.up).normalized;
            //float dotProduct = Vector3.Dot(planarDirToPlayer, characterForwardDir);
            //Debug.Log($"Dot prod {dotProduct}");
            ////if (dotProduct >= .9f)
            ////{
            ////    return true;
            ////}
            //return false;
        }

        public virtual bool HasTraversalInteraction(KCC_Context Context, KCC_TraversalSplineData fromData, out TraversalToTraversalInteractionData result)
        {
            //Base interaction strategy
            result = null;
            
            if (!AllowInteractionWithOtherTraversals || !fromData.PlayerInput.JumpPerformed)
                return false;

            KCC_TraversalSpline interactableSpline = Context.Interactions.SphereCastLookAt<KCC_TraversalSpline>(
                Context.CameraTransform.position,
                Context.CameraTransform.forward,
                30f,
                Context.Controller.SplineTraversalState.TraversablesLayer,
                new[] { this.gameObject },
                OtherTraversalInteractionRange);

            if (interactableSpline == null)
            {
                return false;
            }

            result = TraversalTransitionFactory.Create(this, interactableSpline, fromData, Context.Motor.TransientPosition);
            return true;
        }

        public virtual void Jump(ref Vector3 currentVelocity, KCC_Context Context, float deltaTime)
        {
            Context.Controller.LocomotionState.Jump(ref currentVelocity, Context.Motor.CharacterUp, deltaTime);
            Context.Controller.TransitionToDefaultState();
        }

        public IEnumerator AnchorPlayer(KCC_Context Context, KCC_TraversalSplineData data)
        {
            // Duration of the anchoring animation
            float duration = 0.75f; // Adjust as needed
            float elapsedTime = 0f;

            // Starting and target positions
            Vector3 startPosition = Context.Motor.InitialSimulationPosition;
            Vector3 targetPosition = data.TraversalPosition;

            // Define the movement curve (e.g., move up by 1 unit and back down)
            float peakHeight = 1.0f; // Adjust the peak height as needed

            while (elapsedTime < duration)
            {
                // Calculate normalized time (0 to 1)
                float t = elapsedTime / duration;

                // Use a sine curve to move up and down
                float heightOffset = Mathf.Sin(t * Mathf.PI) * peakHeight;

                // Calculate the new position
                Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, t);
                newPosition.y += heightOffset;

                // Set the player's position
                Context.Motor.SetPosition(newPosition);

                // Increment elapsed time
                elapsedTime += Time.deltaTime;

                // Yield until the next frame
                yield return null;
            }

            // Ensure the final position is set
            Context.Motor.SetPosition(targetPosition);

        }
    }
}
