using System;
using UniRx;
using UnityEngine;

namespace Solitaire.Services
{
    public class PointsService : IPointsService
    {
        private readonly Subject<(int delta, Vector3 worldPos)> _onScored = new();

        public IntReactiveProperty Points { get; } = new();
        public IObservable<(int delta, Vector3 worldPos)> OnScored => _onScored;

        public void Set(int value)
        {
            Points.Value = value;
        }

        public void Add(int value)
        {
            if (value == 0) return;
            Set(Mathf.Max(Points.Value + value, 0));
        }

        public void Add(int value, Vector3 worldPos)
        {
            if (value == 0) return;
            Set(Mathf.Max(Points.Value + value, 0));
            if (value > 0)
                _onScored.OnNext((value, worldPos));
        }

        public void Reset()
        {
            Set(0);
        }
    }
}
