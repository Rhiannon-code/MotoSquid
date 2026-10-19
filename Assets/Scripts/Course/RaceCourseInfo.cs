using MotoSquid.Core;
using UnityEngine;

namespace MotoSquid.Course
{
    // Lives on the built course object. GameSession boots itself before any scene, with the menu's
    // defaults, so a scene played straight from the editor would always run as a point-to-point race
    // whatever its course says. Runs before RaceManager (10) reads the session
    [DefaultExecutionOrder(-20)]
    public class RaceCourseInfo : MonoBehaviour
    {
        public RaceCourse course;

        void Awake()
        {
            var session = GameSession.Instance;
            if (session == null || session.HasTrackSelection || course == null) return;
            session.SelectedTrackType = course.trackType;
            session.SelectedGameMode  = course.gameMode;
        }
    }
}
