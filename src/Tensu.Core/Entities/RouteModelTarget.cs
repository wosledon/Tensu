namespace Tensu.Core.Entities;

/// <summary>
/// 路由模型/影子模型挂载的真实模型关联表。
/// Shadow 模式下 IsActive 标记当前活跃目标；
/// Route 模式下 Priority 决定候选顺序，由 LLM/算法从中路由。
/// </summary>
public class RouteModelTarget
{
    public int Id { get; set; }
    public int RouteModelId { get; set; }
    public int ModelId { get; set; }
    public bool IsActive { get; set; } // shadow 模式：当前活跃目标
    public int Priority { get; set; } // route 模式：候选顺序
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RouteModel RouteModel { get; set; } = null!;
    public Model Model { get; set; } = null!;
}
