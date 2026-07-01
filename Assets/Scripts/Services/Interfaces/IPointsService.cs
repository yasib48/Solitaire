using System;
using UniRx;
using UnityEngine;

namespace Solitaire.Services
{
    public interface IPointsService
    {
        IntReactiveProperty Points { get; }
        IObservable<(int delta, Vector3 worldPos)> OnScored { get; }

        void Add(int value);
        void Add(int value, Vector3 worldPos);
        void Reset();
        void Set(int value);
    }
}
