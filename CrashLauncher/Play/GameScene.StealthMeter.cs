using CrashEngine.Core;
using CrashEngine.Stealth;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    public sealed class GameHUD : Component
    {
        public GameScene? Scene { get; set; }
        private List<StealthEnemy>? _enemies;
        private float _timer;

        public override void OnUpdate()
        {
            _timer += EngineTime.Delta;
            if (_timer < 0.5f) return;
            _timer = 0f;
            _enemies ??= FindAll();
            if (_enemies.Count == 0) return;
            float max  = _enemies.Max(e => e.DetectionPct);
            var   top  = _enemies.OrderByDescending(e => e.DetectionPct).First();
            int   bars = (int)(max / 5f);
            string bar = "[" + new string('|', bars) + new string(' ', 20 - bars) + "]";
            Console.Write($"\r{top.State,-7} {bar} {max,5:F1}%  ({_enemies.Count} enemies)   ");
        }

        private List<StealthEnemy> FindAll()
        {
            if (Scene is null) return new();
            var result = new List<StealthEnemy>();
            foreach (var root in Scene.Roots)
                Collect(root, result);
            return result;
        }

        private static void Collect(Entity e, List<StealthEnemy> out_)
        {
            var en = e.Get<StealthEnemy>(); if (en != null) out_.Add(en);
            foreach (var child in e.Children) Collect(child, out_);
        }
    }
}
