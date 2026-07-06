using UnityEngine;

namespace Solitaire.Services
{
    public class TimerService : ITimerService
    {
        public float Elapsed { get; private set; }
        public int ElapsedSeconds => Mathf.FloorToInt(Elapsed);

        public void Reset()
        {
            Elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            Elapsed += deltaTime;
        }
    }
}
