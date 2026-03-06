namespace Game.Tests.Lanes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class UnitLaneAttribute() : TraitAttribute("Lane", "unit");

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class IntegrationLaneAttribute() : TraitAttribute("Lane", "integration");

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ReplayLaneAttribute() : TraitAttribute("Lane", "replay");

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CanaryLaneAttribute() : TraitAttribute("Lane", "canary");
