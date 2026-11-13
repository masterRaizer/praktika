using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Функции плавности (0..1 → 0..1).</summary>
    public static class Ease
    {
        public static float Linear(float t) { return t; }
        public static float InCubic(float t) { return t * t * t; }
        public static float OutCubic(float t) { float u = 1 - t; return 1 - u * u * u; }
        public static float OutQuint(float t) { float u = 1 - t; return 1 - u * u * u * u * u; }
        public static float InOutCubic(float t) { return t < 0.5f ? 4 * t * t * t : 1 - (float)Math.Pow(-2 * t + 2, 3) / 2; }
        public static float OutExpo(float t) { return t >= 1 ? 1 : 1 - (float)Math.Pow(2, -10 * t); }

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            float u = t - 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return (float)(Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * (2 * Math.PI / 3)) + 1);
        }
    }

    /// <summary>
    /// Единый таймер анимаций приложения (~60 кадров/с). Анимации считаются по реальному времени,
    /// поэтому остаются плавными, даже если отдельный кадр задержался.
    /// </summary>
    public static class Animator
    {
        private sealed class Tween
        {
            public object Owner;
            public string Key;
            public float From, To;
            public double Start, Duration;
            public Func<float, float> Easing;
            public Action<float> Update;
            public Action Done;
            public bool Dead;
        }

        private static readonly List<Tween> tweens = new List<Tween>();
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static Timer timer;
        private static double last;

        /// <summary>Вызывается каждый кадр; аргумент — прошедшее время в секундах.</summary>
        public static event Action<float> Frame;

        public static double Now { get { return clock.Elapsed.TotalSeconds; } }

        /// <summary>Анимации выключены (режим снимков экрана): значения применяются сразу.</summary>
        public static bool Instant;

        public static void Start()
        {
            if (timer != null) return;
            timer = new Timer { Interval = 15 };
            timer.Tick += delegate { Step(); };
            last = Now;
            timer.Start();
        }

        public static void Run(object owner, string key, float from, float to, int ms,
                               Func<float, float> easing, Action<float> update, Action done = null, int delayMs = 0)
        {
            Start();
            Cancel(owner, key);
            if (Instant || (ms <= 0 && delayMs <= 0))
            {
                update(to);
                if (done != null) done();
                return;
            }
            tweens.Add(new Tween
            {
                Owner = owner, Key = key, From = from, To = to,
                Start = Now + delayMs / 1000.0, Duration = Math.Max(1, ms) / 1000.0,
                Easing = easing, Update = update, Done = done
            });
            update(from);
        }

        /// <summary>Отложенный вызов на потоке интерфейса.</summary>
        public static void After(object owner, string key, int ms, Action action)
        {
            Run(owner, key, 0, 1, ms, null, delegate { }, action);
        }

        public static void Cancel(object owner, string key = null)
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                Tween t = tweens[i];
                if (ReferenceEquals(t.Owner, owner) && (key == null || t.Key == key))
                {
                    t.Dead = true;
                    tweens.RemoveAt(i);
                }
            }
        }

        public static bool IsRunning(object owner, string key)
        {
            foreach (Tween t in tweens)
                if (ReferenceEquals(t.Owner, owner) && t.Key == key) return true;
            return false;
        }

        private static void Step()
        {
            double now = Now;
            float dt = (float)(now - last);
            last = now;
            if (dt > 0.1f) dt = 0.1f;

            if (tweens.Count > 0)
            {
                Tween[] snapshot = tweens.ToArray();
                foreach (Tween t in snapshot)
                {
                    if (t.Dead || now < t.Start) continue;
                    float p = (float)((now - t.Start) / t.Duration);
                    bool finished = p >= 1;
                    if (finished)
                    {
                        p = 1;
                        t.Dead = true;
                        tweens.Remove(t);
                    }
                    try
                    {
                        float e = t.Easing != null ? t.Easing(p) : p;
                        t.Update(t.From + (t.To - t.From) * e);
                        if (finished && t.Done != null) t.Done();
                    }
                    catch (Exception ex)
                    {
                        t.Dead = true;
                        tweens.Remove(t);
                        Debug.WriteLine("Animator: " + ex);
                    }
                }
            }

            Action<float> f = Frame;
            if (f != null)
            {
                try { f(dt); }
                catch (Exception ex) { Debug.WriteLine("Animator.Frame: " + ex); }
            }
        }
    }

    /// <summary>Число, плавно стремящееся к заданному значению; при изменении перерисовывает владельца.</summary>
    public sealed class AnimValue
    {
        private readonly Control owner;
        private float target;
        public float Value;

        public AnimValue(Control owner, float initial = 0)
        {
            this.owner = owner;
            Value = target = initial;
        }

        public float Target { get { return target; } }

        public void To(float value, int ms = 170, Func<float, float> easing = null)
        {
            if (Math.Abs(value - target) < 1e-5f) return;
            target = value;
            Animator.Run(this, "v", Value, value, ms, easing ?? Ease.OutCubic, delegate(float v)
            {
                Value = v;
                if (!owner.IsDisposed) owner.Invalidate();
            });
        }

        public void Set(float value)
        {
            Animator.Cancel(this);
            Value = target = value;
            if (!owner.IsDisposed) owner.Invalidate();
        }
    }
}
