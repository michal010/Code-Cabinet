using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class Cherry : MyGameObject
{

    [Inject]
    IGameController _gameController;
    [Inject]
    SignalBus _signalBus;

    List<FruitType> _affectedFruits;

    private void Awake()
    {
        GUID = Guid.NewGuid().ToString();
        _affectedFruits = DataManager.GameData.GetBuff("Cherry").GetUpgrade(UpgradeType.Effectiveness).CurrentLevelUpgrdeData().AffectedFruits;
    }

    public override void OnCollision(Collision2D collision)
    {
        if (_gameController.GameOver)
            return;
        if (collision.transform.parent == transform)
            return;
        collision.gameObject.TryGetComponent<CollisionHandler>(out var otherCollisionHandler);
        if (otherCollisionHandler == null)
            return;
        collision.transform.parent.TryGetComponent<Fruit>(out var fruit);
        //Fruit otherFruit = otherCollisionHandler.Fruit;
        if (fruit == null || fruit.isCombined)
            return;

        if (!_affectedFruits.Contains(fruit.Data.FruitType))
            return;

        fruit.isCombined = true;

        Vector2 meanPos = (fruit.transform.position + this.transform.position) / 2;

        _gameController.RemoveFruit(fruit);
        //Destroy(fruit.gameObject);
        Destroy(this.gameObject);

        Vector2 spawnPos = new Vector3(meanPos.x, meanPos.y, 0);

        var newFruit = _gameController.SpawnFruit(spawnPos, fruit.CombinedInto.Prefab, false);

        newFruit.Parent1GUID = fruit.GUID;
        newFruit.Parent2GUID = fruit.GUID;

        // FIRE COMBINATION SIGNAL
        _signalBus.Fire<FruitMergedSignal>(new FruitMergedSignal(null, fruit, newFruit));
    }
}
