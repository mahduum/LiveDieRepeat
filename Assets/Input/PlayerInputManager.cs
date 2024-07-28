using System;
using System.Collections;
using System.Collections.Generic;
using Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Ray = UnityEngine.Ray;

public enum InputMode
{
    None,
    SpawnPlacement,
    SplineEdit
}

public class PlayerInputManager : MonoBehaviour
{
    [SerializeField] private PlayerInput _playerInput;
    [SerializeField] private InputAction _inputAction;
    [SerializeField] private Camera _camera;
    [SerializeField] private float _debugSphereRadius = .25f;
    [SerializeField] private int _spawnObjectIndex = 0;

    [SerializeField] private InputMode _inputMode = InputMode.SplineEdit;
    [SerializeField] private GameObject _splineHandlePrefab;

    private Vector3? _clickedCoord;

    private Entity _placementInputEntity;
    private Entity _screenToRayEntity;
    private Entity _splineHandleEntity;
    private World _world;

    private void OnEnable()
    {
        // _inputAction.started += SpawnObject;
        // _inputAction.Enable();
        
        _camera ??= Camera.main;
        
        _world = World.DefaultGameObjectInjectionWorld;
    }
    
    public void SpawnObject(InputAction.CallbackContext ctx)
    {
        Vector2 screenPosition = ctx.ReadValue<Vector2>();
        Ray ray = _camera.ScreenPointToRay(screenPosition);
        _clickedCoord = ray.GetPoint(_camera.farClipPlane);

        if (_world.IsCreated && _world.EntityManager.Exists(_placementInputEntity) == false)
        {
            _placementInputEntity = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddBuffer<PlacementInput>(_placementInputEntity);
        }

        RaycastInput input = new RaycastInput()
        {
            Start = ray.origin,
            Filter = CollisionFilter.Default,
            End = ray.GetPoint(_camera.farClipPlane)
        };

        //todo base on the input mode -> change input bindings depending on the mode, for example edit curve
        //for now workaround:

        _world.EntityManager.GetBuffer<PlacementInput>(_placementInputEntity).Add(new PlacementInput()
        {
            Value = input,
            SpawnObjectIndex = _spawnObjectIndex
        });
        
    }
    
    public void MouseUnClicked(InputAction.CallbackContext ctx)
    {
        _clickedCoord = null;
    }

    public void ShowSplineHandle(InputAction.CallbackContext ctx)
    {
        // if (ctx.started)
        //     Debug.Log("Action was started");
        // else if (ctx.performed)
        //     Debug.Log("Action was performed");
        // else if (ctx.canceled)
        //     Debug.Log("Action was cancelled");
        
        if (_world.IsCreated == false)
        {
            return;
        }

        bool entityExists = _world.EntityManager.Exists(_screenToRayEntity);
        
        bool isTriggering = ctx.action.IsInProgress();

        if (isTriggering)
        {
            Vector2 screenPosition = ctx.ReadValue<Vector2>();
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            _clickedCoord = ray.GetPoint(_camera.farClipPlane);

            if (entityExists == false)
            {
                _screenToRayEntity = _world.EntityManager.CreateEntity();
                _world.EntityManager.AddComponent<ScreenPointToRayComponent>(_screenToRayEntity);
                _world.EntityManager.AddComponent<ShowSplineHandleComponent>(_screenToRayEntity);
                _world.EntityManager.AddComponent<PositionDelta>(_screenToRayEntity);
            }
            
            RaycastInput input = new RaycastInput()
            {
                Start = ray.origin,
                Filter = CollisionFilter.Default,
                End = ray.GetPoint(_camera.farClipPlane)
            };
            
            _world.EntityManager.SetComponentData(_screenToRayEntity, new ScreenPointToRayComponent()//todo or I can query this point based on what additional components it has, always set the value and attach or enable marker component?
            {
                Value = input
            });
            
            _world.EntityManager.SetComponentEnabled<ScreenPointToRayComponent>(_screenToRayEntity, true);
            _world.EntityManager.SetComponentEnabled<ShowSplineHandleComponent>(_screenToRayEntity, true);
        }
        else
        {
            if (entityExists)
            {
                _world.EntityManager.SetComponentEnabled<ScreenPointToRayComponent>(_screenToRayEntity, false);
            }
        }
    }

    public void SetControlPoint()
    {
        /*
         * 1. On start set the first control point.
         * 2. On stop dragging set the second control point.
         * 3. On next click the third control point is set to be length of 1 pointing towards first control point, and the fourth is set when the click happened.
         * 4. On click that starts new bezier, the last incoming tangent interpolates between some new position data and the weight of outgoing tangent?
         */
    }

    private void OnDrawGizmos()
    {
        if (_clickedCoord.HasValue == false) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(_clickedCoord.Value, _debugSphereRadius);
    }

    private void OnDisable()
    {
        // _inputAction.started -= SpawnObject;
        // _inputAction.Disable();
        
        if (_world is {IsCreated: true} && _world.EntityManager.Exists(_placementInputEntity))
        {
            _world.EntityManager.DestroyEntity(_placementInputEntity);
        }
        
        if (_world is {IsCreated: true} && _world.EntityManager.Exists(_screenToRayEntity))
        {
            _world.EntityManager.DestroyEntity(_screenToRayEntity);
        }
    }
}

public struct PlacementInput : IBufferElementData//todo use this only as spawn object index and leave raycastinput to screentoraycomp
{
    public RaycastInput Value;
    public int SpawnObjectIndex;
}

public struct ScreenPointToRayComponent : IComponentData, IEnableableComponent
{
    public RaycastInput Value;
    //optionally it may have an entity to which this data should be applied to? data is calculated, and then is passed onto this entity, handle point data component?
}

public struct ShowSplineHandleComponent : IComponentData, IEnableableComponent
{
}

public struct PositionDelta : IComponentData
{
    public float3 Previous;
    public float3 Current;
}

public struct MoveDirection : IComponentData
{
    public float3 Value;
}
