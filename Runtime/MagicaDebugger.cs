using UnityEngine;
using UniVRM10;

namespace VRM2Magica.Runtime
{
    public class MagicaDebugger : MonoBehaviour
    {
        [SerializeField] private Vrm10Instance? vrm10Instance;
        
        [ContextMenu("Run")]
        private void Start()
        {
            if (vrm10Instance == null)
            {
                return;
            }
            
            Vrm10ToMagicaConverter.Convert(vrm10Instance);
        }
    }
}