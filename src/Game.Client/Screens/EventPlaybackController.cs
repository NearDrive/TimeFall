using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Microsoft.Xna.Framework;

namespace Game.Client.Screens;

public sealed class EventPlaybackController
{
    private const float DefaultEventDurationSeconds = 0.12f;
    private const float DamageEventDurationSeconds = 0.7f;
    private const float CardHighlightDurationSeconds = 0.45f;
    private const int MaxRecentEvents = 16;

    private readonly Queue<GameEvent> _eventQueue = new();
    private readonly List<GameEvent> _recentEvents = new();
    private ActiveEventPlayback? _activePlayback;

    public ActiveEventPlayback? ActivePlayback => _activePlayback;
    public IReadOnlyList<GameEvent> RecentEvents => _recentEvents;

    public void EnqueueRange(IEnumerable<GameEvent> events)
    {
        foreach (var gameEvent in events)
        {
            _eventQueue.Enqueue(gameEvent);
            AddRecentEvent(gameEvent);
        }
    }

    public void Update(GameTime gameTime)
    {
        if (_activePlayback is not null)
        {
            var nextRemaining = _activePlayback.RemainingSeconds - (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (nextRemaining > 0f)
            {
                _activePlayback = _activePlayback with { RemainingSeconds = nextRemaining };
                return;
            }

            _activePlayback = null;
        }

        if (_eventQueue.Count > 0)
        {
            _activePlayback = BuildPlayback(_eventQueue.Dequeue());
        }
    }

    public void Reset()
    {
        _eventQueue.Clear();
        _recentEvents.Clear();
        _activePlayback = null;
    }

    private void AddRecentEvent(GameEvent gameEvent)
    {
        _recentEvents.Add(gameEvent);
        if (_recentEvents.Count > MaxRecentEvents)
        {
            _recentEvents.RemoveAt(0);
        }
    }

    private static ActiveEventPlayback BuildPlayback(GameEvent gameEvent)
    {
        return gameEvent switch
        {
            PlayerStrikePlayed strike => new ActiveEventPlayback(
                gameEvent,
                DamageEventDurationSeconds,
                new DamageFeedbackVisual(
                    DamageFeedbackTarget.EnemyArea,
                    Math.Max(0, strike.EnemyHpBeforeHit - strike.EnemyHpAfterHit)),
                new CardHighlightVisual(strike.Card.DefinitionId)),

            EnemyAttackPlayed enemyAttack => new ActiveEventPlayback(
                gameEvent,
                DamageEventDurationSeconds,
                new DamageFeedbackVisual(
                    DamageFeedbackTarget.Player,
                    Math.Max(0, enemyAttack.PlayerHpBeforeHit - enemyAttack.PlayerHpAfterHit)),
                null),

            StatusTriggered triggered when triggered.HpAfter < triggered.HpBefore => new ActiveEventPlayback(
                gameEvent,
                DamageEventDurationSeconds,
                new DamageFeedbackVisual(
                    triggered.Target == TurnOwner.Player ? DamageFeedbackTarget.Player : DamageFeedbackTarget.EnemyArea,
                    triggered.HpBefore - triggered.HpAfter),
                null),

            CardDiscarded discarded => new ActiveEventPlayback(
                gameEvent,
                CardHighlightDurationSeconds,
                null,
                new CardHighlightVisual(discarded.Card.DefinitionId)),

            _ => new ActiveEventPlayback(
                gameEvent,
                DefaultEventDurationSeconds,
                null,
                null),
        };
    }
}

public enum DamageFeedbackTarget
{
    Player,
    EnemyArea,
}

public sealed record DamageFeedbackVisual(DamageFeedbackTarget Target, int Amount);

public sealed record CardHighlightVisual(CardId CardId);

public sealed record ActiveEventPlayback(
    GameEvent Event,
    float RemainingSeconds,
    DamageFeedbackVisual? DamageFeedback,
    CardHighlightVisual? CardHighlight);
