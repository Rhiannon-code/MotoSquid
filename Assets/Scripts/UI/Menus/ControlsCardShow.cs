using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MotoSquid.UI
{
    // Plays the controls cards on the loading screen, one after another, cross-fading between them.
    // Its own component rather than more of SceneLoader: that one owns the load and the bar, this one
    // owns what is on screen while it runs.
    public class ControlsCardShow : MonoBehaviour
    {
        [Tooltip("Shown in order. Gamepad first, then keyboard.")]
        public Image[] cards;

        public float secondsPerCard = 4f;
        public float crossFade      = 0.5f;

        // A load stall can freeze the screen for seconds; counting it would take that time off whichever
        // card happened to be up, so no single frame counts for more than this
        public float maxCountedFrame = 0.1f;

        public bool  Finished { get; private set; }

        bool _stopAtRoundEnd;

        // The cards alternate while the load runs. Played once, the last card stayed up for the whole rest
        // of the load and got several times the first card's time; stopping only at the end of a round
        // gives every card the same number of turns
        public void StopAtRoundEnd() => _stopAtRoundEnd = true;

        // How far through the current round of cards, so the loading bar can count down the wait for it
        public float RoundProgress => cards == null || cards.Length == 0 ? 1f
            : Mathf.Clamp01(_roundElapsed / (cards.Length * secondsPerCard));
        float _roundElapsed;

        // What SceneLoader has to keep the screen up for. The cross-fades run inside each card's time,
        // so this is exactly what the viewer gets.
        public float TotalDuration => cards != null ? cards.Length * secondsPerCard : 0f;

        void Awake()
        {
            if (cards == null || cards.Length == 0)
            {
                Debug.LogWarning("[ControlsCardShow] No cards assigned, the loading screen will be blank.", this);
                Finished = true;
                enabled  = false;
                return;
            }

            foreach (var card in cards)
                if (card != null) SetAlpha(card, 0f);
        }

        void Start() => StartCoroutine(Play());

        IEnumerator Play()
        {
            Image outgoing = null;
            do
            {
                _roundElapsed = 0f;
                foreach (Image incoming in cards)
                {
                    float elapsed = 0f;
                    while (elapsed < crossFade)
                    {
                        elapsed += CountedDelta();
                        float t = crossFade > 0f ? Mathf.Clamp01(elapsed / crossFade) : 1f;
                        SetAlpha(incoming, t);
                        if (outgoing != incoming) SetAlpha(outgoing, 1f - t);
                        yield return null;
                    }
                    SetAlpha(incoming, 1f);
                    if (outgoing != incoming) SetAlpha(outgoing, 0f);

                    float hold = 0f;
                    while (hold < secondsPerCard - crossFade)
                    {
                        hold += CountedDelta();
                        yield return null;
                    }
                    outgoing = incoming;
                }
            }
            while (!_stopAtRoundEnd);

            Finished = true;
        }

        float CountedDelta()
        {
            float delta = Mathf.Min(Time.unscaledDeltaTime, maxCountedFrame);
            _roundElapsed += delta;
            return delta;
        }

        static void SetAlpha(Image image, float alpha)
        {
            if (image == null) return;
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }
    }
}
