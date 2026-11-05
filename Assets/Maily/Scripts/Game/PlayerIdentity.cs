using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Maily
{
    /// <summary>Anonymous player identity, started at bootstrap and never blocked on.
    /// The root that cloud saves / leaderboards can build on later.</summary>
    public class PlayerIdentity : MonoBehaviour
    {
        public static string PlayerId { get; private set; }

        async void Start()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                Debug.Log($"[Maily] Signed in anonymously. PlayerId={PlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Maily] Player services unavailable, playing offline: {e.Message}");
            }
        }
    }
}
