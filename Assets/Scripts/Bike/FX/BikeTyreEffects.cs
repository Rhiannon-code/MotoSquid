using UnityEngine;

namespace MotoSquid.Bike
{
    // Tyre slip, skidmarks and tyre smoke, all driven from how much each wheel is sliding.
    public sealed class BikeTyreEffects
    {
        const int SkidmarkInterval = 4;
        const int WheelSlipInterval = 3;
        const int TyreSmokeInterval = 3;
        const float SkidmarkMinSpeed = 3f;

        readonly BikeController bike;
        ParticleSystem smoke;
        ParticleSystem.MainModule smokeMain;
        ParticleSystem.EmissionModule smokeEmission;
        ParticleSystem.ShapeModule smokeShape;
        // Base values from the prefab, so burnout scaling multiplies up from them instead of overwriting them
        float smokeBaseRadius, smokeBaseRate, smokeBaseSize;

        float slipFront, slipRear;
        int skidmarkIndexFront = -1, skidmarkIndexRear = -1;
        int skidmarkCounter, wheelSlipCounter, smokeCounter;

        public BikeTyreEffects(BikeController bike) => this.bike = bike;

        public SkidmarkController Skidmarks { get; private set; }
        public float SkidIntensity01 => Mathf.Max(slipFront, slipRear);

        public void CreateEffects()
        {
            var refs = bike.bikeReferences;
            if (refs.skidmarksPrefab != null)
            {
                var instance = Object.Instantiate(refs.skidmarksPrefab);
                Skidmarks = instance?.GetComponent<SkidmarkController>();
            }

            if (refs.tireSmokePrefab != null)
            {
                var instance = Object.Instantiate(refs.tireSmokePrefab, refs.RearWheel);
                instance.transform.localPosition = new Vector3(0, bike.bikeGeometry.RearWheelRadius, 0);
                smoke = instance?.GetComponent<ParticleSystem>();
                if (smoke != null)
                {
                    smokeMain = smoke.main;
                    smokeEmission = smoke.emission;
                    smokeShape = smoke.shape;
                    smokeBaseRadius = smokeShape.radius;
                    smokeBaseRate = smokeEmission.rateOverTimeMultiplier;
                    smokeBaseSize = smokeMain.startSizeMultiplier;
                }
                smoke?.Stop();
            }
        }

        public void Start()
        {
            if (Skidmarks != null)
                Skidmarks.SkidmarkWidth = bike.bikeGeometry.RearWheelWidth;
        }

        public void Tick()
        {
            if (++smokeCounter < TyreSmokeInterval) return;
            smokeCounter = 0;
            UpdateSmoke();
        }

        public void FixedTick()
        {
            if (++wheelSlipCounter >= WheelSlipInterval)
            {
                wheelSlipCounter = 0;
                CalculateWheelSlips();
            }

            if (++skidmarkCounter >= SkidmarkInterval)
            {
                skidmarkCounter = 0;
                if (bike.CachedSpeed >= SkidmarkMinSpeed)
                    UpdateSkidmarks();
            }
        }

        void CalculateWheelSlips()
        {
            var drivetrain = bike.Drivetrain;
            var refs = bike.bikeReferences;
            Vector3 up = bike.Ground.ProjectedUp;
            float maxSpeed = bike.bikeSettings.maxSpeed;

            float forwardFront, forwardRear;
            if (drivetrain.IsApplyingBrake) { forwardFront = 1; forwardRear = 1; }
            else if (drivetrain.IsApplyingHandBrake || bike.isDoingBurnout) { forwardFront = 0; forwardRear = 1; }
            else { forwardFront = 0; forwardRear = 0; }

            float sideFront = SideSlip(refs.FrontWheelParent, up, maxSpeed);
            float sideRear  = SideSlip(refs.RearWheelParent,  up, maxSpeed);

            slipFront = bike.CachedSpeed < 1f ? 0 : Mathf.Abs((forwardFront + sideFront) / 2);
            slipRear  = bike.CachedSpeed < 1f ? 0 : Mathf.Abs((forwardRear  + sideRear)  / 2);

            if (bike.isDoingBurnout) slipRear = Mathf.Abs((forwardRear + sideRear) / 2);

            // Drift adds significant rear wheel slip regardless of forward slip
            if (bike.isDrifting)
                slipRear = Mathf.Max(slipRear, bike.DriftIntensity * 0.8f);

            if (!bike.bikeIsGrounded) { slipFront = 0; slipRear = 0; }
        }

        float SideSlip(Transform wheelParent, Vector3 up, float maxSpeed)
        {
            Vector3 sideways = Vector3.ProjectOnPlane(wheelParent.right, up).normalized;
            float sidewaysSpeed = Vector3.Dot(bike.bikeReferences.BikeRb.GetPointVelocity(wheelParent.position), sideways);
            return Mathf.Abs(sidewaysSpeed) < 0.01f ? 0 : sidewaysSpeed / maxSpeed;
        }

        void UpdateSkidmarks()
        {
            if (Skidmarks == null) return;
            var ground = bike.Ground;

            skidmarkIndexFront = bike.frontWheelIsGrounded
                ? Skidmarks.AddSkidMark(ground.FrontHit.point, ground.NormalFront, slipFront, skidmarkIndexFront)
                : -1;
            skidmarkIndexRear = bike.rearWheelIsGrounded
                ? Skidmarks.AddSkidMark(ground.RearHit.point, ground.NormalRear, slipRear, skidmarkIndexRear)
                : -1;
        }

        void UpdateSmoke()
        {
            if (smoke == null) return;

            if (bike.isDoingBurnout && bike.burnoutSmokeSuppressed) { smoke.Stop(); return; }

            float intensity = slipRear;
            // Boost smoke during drift for better visual feedback
            if (bike.isDrifting)
                intensity = Mathf.Max(intensity, bike.DriftIntensity * 0.7f);

            if (intensity <= BikeController.SMOKE_SLIP_THRESHOLD)
            {
                smoke.Stop();
                return;
            }

            smoke.Play();
            smokeMain.startSpeed = Mathf.Lerp(1f, 3f, intensity);

            // Burnout build up, the longer you hold the burnout (launch charge 0-1), the denser,
            // thicker and wider the smoke. Regular skids leave it at the prefab's base (charge 0)
            var set = bike.bikeSettings;
            float burn = bike.isDoingBurnout ? bike.LaunchCharge : 0f;
            smokeMain.startSizeMultiplier        = smokeBaseSize   * Mathf.Lerp(1f, set.burnoutSmokeMaxSize,   burn);
            smokeEmission.rateOverTimeMultiplier = smokeBaseRate   * Mathf.Lerp(1f, set.burnoutSmokeMaxRate,   burn);
            smokeShape.radius                    = smokeBaseRadius * Mathf.Lerp(1f, set.burnoutSmokeMaxRadius, burn);
        }
    }
}
