using System;
using UnityEngine;

/// <summary>Global game events so systems (score, UI, audio) stay decoupled.</summary>
public static class GameEvents
{
    public static event Action<EnemyAI> EnemyKilled;
    public static event Action<Vector3, int> CoinsDropped;
    public static event Action<string> Notification;   // short HUD message
    public static event Action PlayerDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        EnemyKilled = null; CoinsDropped = null; Notification = null; PlayerDied = null;
    }

    public static void RaiseEnemyKilled(EnemyAI e) => EnemyKilled?.Invoke(e);
    public static void RaiseCoinsDropped(Vector3 p, int n) => CoinsDropped?.Invoke(p, n);
    public static void Notify(string message) => Notification?.Invoke(message);
    public static void RaisePlayerDied() => PlayerDied?.Invoke();
}
