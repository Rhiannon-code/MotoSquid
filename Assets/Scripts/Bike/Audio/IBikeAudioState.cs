using MotoSquid.Combat;
namespace MotoSquid.Bike
{
    public interface IBikeAudioState
    {
        float SpeedMs { get; }            
        float SkidIntensity01 { get; }    
        bool  IsGrounded { get; }
        bool  CanMove { get; }           
        bool  IsDoingBurnout { get; }    
        bool  IsRevving { get; }          
        float ThrottleInput { get; }     
        float ReverseInput { get; }     
        int   CurrentGear { get; }
        int[] GearSpeeds { get; }
        float MaxSpeedKmh { get; }       
        float SteerAmount { get; }        
        bool  IsDoingWheelie { get; }    
        bool  IsBraking { get; }          

        event System.Action GearChanged;
        event System.Action<bool> EngineMuteRequested;
    }
}
