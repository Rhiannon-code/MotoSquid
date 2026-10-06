namespace MotoSquid.Bike
{
    public interface IEngineAudio
    {
        float EngineRpm01 { get; }       // 0..1 for the HUD tacho
        bool  EngineAtLimiter { get; }   // HUD redline flash
        void  SetEngineMuted(bool muted); // Grid fly in/finish (RaceManager)
        void  SilenceEngine();            // Crash recovery (RagdollActivator)
    }
}
