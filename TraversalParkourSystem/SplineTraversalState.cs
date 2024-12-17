using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace Traversal
{
    public enum KCCTraversalStates { None, Anchoring, Traversing, Transitioning}

    [System.Serializable]
    public class SplineTraversalState : KCC_State
    {
        [field:SerializeField] public LayerMask TraversablesLayer { get; set; }
        [field: SerializeField] public float SplineInteractionRange { get; set; } = 10f;
        private IKCC_TraversalSpline _activeTraversal;
        public IKCC_TraversalSpline ActiveTraversal 
        { 
            get { return _activeTraversal; } 
            set { _activeTraversal = value; } 
        }

        //Data
        KCC_TraversalSplineData _traversalData;
        public KCC_TraversalSplineData TraversalData 
        { 
            get 
            {
                if(_traversalData == null)
                {
                    _traversalData = _activeTraversal.CreateTraversalData();
                }
                return _traversalData;
            } 
            set { _traversalData = value; } 
        }

        protected KCCTraversalStates traversalState;

        // Other
        [SerializeField] bool DebuggingEnabled = true;

        // Traversal - to - traversal
        IEnumerator traversalMethod;
        IEnumerator anchoringMethod;

        protected bool _jumpRequested = false;
        protected Vector3 jumpTargetPosition = Vector3.zero;
        protected Vector3 jumpFromPosition = Vector3.zero;
        protected KCC_TraversalSpline jumpTargetTraversal;
        protected float jumpTime = 0f;

        public override void OnStateEnter()
        {
            Motor.SetCapsuleCollisionsActivation(false);
            Motor.SetMovementCollisionsSolvingActivation(false);
            Motor.SetGroundSolvingActivation(false);
            anchoringMethod = _activeTraversal.AnchorPlayer(Context, TraversalData);
            traversalState = KCCTraversalStates.Anchoring;
            _activeTraversal.OnTraversalEnter(Context, TraversalData);
            //Reset jump
            _jumpRequested = false;
            jumpTime = 0f;
        }

        public override void OnStateExit()
        {
            Motor.SetCapsuleCollisionsActivation(true);
            Motor.SetMovementCollisionsSolvingActivation(true);
            Motor.SetGroundSolvingActivation(true);
            _traversalData = null;
            _activeTraversal = null;
        }

        public void ChangeTraversal(IKCC_TraversalSpline traversal)
        {
            _traversalData = null;
            _activeTraversal.OnTraversalExit(Context);
            ActiveTraversal = traversal;
            _activeTraversal.OnTraversalEnter(Context, TraversalData);
            anchoringMethod = _activeTraversal.AnchorPlayer(Context, TraversalData);
            traversalState = KCCTraversalStates.Anchoring;
        }

        public override void SetInputs(ref PlayerCharacterInputs inputs, Vector3 moveInputVector, Vector3 cameraPlanarDirection, Quaternion cameraPlanarRotation, Vector3 worldPlanarInputVector)
        {
            if(ActiveTraversal == null) return;
            ActiveTraversal.MapTraversalInput(TraversalData, inputs, moveInputVector, cameraPlanarDirection, cameraPlanarRotation, worldPlanarInputVector);
            if (inputs.JumpPerformed)
                _jumpRequested = true;
        }

        public override void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            if (ActiveTraversal == null) return;
            currentVelocity = Vector3.zero;
            switch (traversalState)
            {
                case KCCTraversalStates.None:
                    break;
                case KCCTraversalStates.Anchoring:
                    Anchor();
                    break;
                case KCCTraversalStates.Traversing:
                    if (_jumpRequested)
                    {
                        //Default jump handler
                        ActiveTraversal.Jump(ref currentVelocity, Context, deltaTime);
                        return;
                    }
                    
                    ActiveTraversal.UpdatePosition(ref currentVelocity, Context, TraversalData, deltaTime);
                    break;
                case KCCTraversalStates.Transitioning:
                    TravelToNextTraversal();
                    break;
            }
        }

        public override void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (ActiveTraversal == null) return;
            //base.UpdateRotation(ref currentRotation, deltaTime);
            ActiveTraversal.UpdateRotation(ref currentRotation, Context, TraversalData, deltaTime);
        }

        public override void BeforeCharacterUpdate(float deltaTime)
        {
            if (ActiveTraversal == null) return;

            if (traversalState == KCCTraversalStates.Transitioning || traversalState == KCCTraversalStates.Anchoring)
                return;

            if (TraversalData.TraversalInput.magnitude > 0)
                ActiveTraversal.EvaluateNextTraversalProgress(Context, TraversalData, deltaTime);
            // Check if start or end reached
            if (TraversalData.TraversalEval == 0)
            {
                ActiveTraversal.AtTraversalZeroReached(Context, TraversalData);
            }
            else if (TraversalData.TraversalEval == 1)
            {
                ActiveTraversal.AtTraversalOneReached(Context, TraversalData);
            }
            //Make sure player is still in traversal state, and active traversal is not null here. it is needed 
            // TODO: make function in this class handling transition from traversal spline to locomotion state.
            if (ActiveTraversal == null) return;

            // Fetch possible interaction and determine transition strategy
            if (ActiveTraversal.HasTraversalInteraction(Context, TraversalData, out var result))
            {
                traversalMethod = result.traversalTransitionStrategy.PerformTransition(
                    result.fromTraversal,
                    result.toTraversal,
                    result.fromData,
                    result.toData,
                    Context);

                traversalState = KCCTraversalStates.Transitioning;
            }
        }

        void Anchor()
        {
            if (anchoringMethod != null)
            {
                if (!anchoringMethod.MoveNext())
                {
                    traversalState = KCCTraversalStates.Traversing;
                    anchoringMethod = null;
                }
            }
        }

        void TravelToNextTraversal()
        {
            if (traversalMethod != null)
            {
                if (!traversalMethod.MoveNext())
                {
                    traversalState = KCCTraversalStates.Traversing;
                    traversalMethod = null;
                }
            }
        }

        public void CheckPlayerInteractionWithTraversal(ref PlayerCharacterInputs inputs)
        {
            KCC_TraversalSpline traversal = Context.Interactions.GetClosestRaycastHit<KCC_TraversalSpline>(out var hit,
                Context.Controller.SplineTraversalState.TraversablesLayer,
                Context.CameraTransform.position,
                Context.CameraTransform.forward,
                5f,
                0.5f);
            // New system
            if (traversal != null)
            {
                var tmpData = traversal.CreateTraversalData();
                traversal.GetClosestSpline(Context.Motor.TransientPosition);
                traversal.EvaluateTraversalAtPoint(Context.Motor.TransientPosition, tmpData);
                // Sprawdzenie scenariuszy traversal
                if (traversal.TryEvaluateDistanceInteractionScenarios(Context, tmpData, inputs, out var scenario))
                {
                    Debug.Log($"Entering traversal using scenario: {scenario.ScenarioName}");

                    // Ustawienie aktywnego traversal
                    ActiveTraversal = traversal;

                    // animacja w stylu: TODO: przenieœæ to do scenariusza, tak aby ka¿dy mia³ potencjalnie inn¹ animkê
                    //Context.Controller.CrossFade(scenario.AnimationStateName, scenario.TransitionDuration);

                    // przejscie do traversalu
                    Context.Controller.TransitionToState(Context.Controller.SplineTraversalState);
                }
            }
        }

        public void CheckPlayerLocomotionMovementHit(Collider hitCollider, ref PlayerCharacterInputs inputs)
        {

            // Spline check
            if (hitCollider.TryGetComponent<KCC_TraversalSpline>(out var traversal))
            {
                var tmpData = traversal.CreateTraversalData();
                traversal.GetClosestSpline(Context.Motor.TransientPosition);
                traversal.EvaluateTraversalAtPoint(Context.Motor.TransientPosition, tmpData);

                // Sprawdzenie scenariuszy traversal
                if (traversal.TryEvaluateCollisionInteractionScenarios(Context, tmpData, inputs, out var scenario))
                {
                    Debug.Log($"Entering traversal using scenario: {scenario.ScenarioName}");

                    // Ustawienie aktywnego traversala

                    ActiveTraversal = traversal;

                    // animacja w stylu:
                    //Context.Controller.CrossFade(scenario.AnimationStateName, scenario.TransitionDuration);

                    // przejscie do traversalu
                    Context.Controller.TransitionToState(Context.Controller.SplineTraversalState);
                }

            }
        }
    }
}
