using UnityEngine;

// Classification optionnelle des meshes crees par le generateur de carte.
[DisallowMultipleComponent]
public sealed class RadarMapElement : MonoBehaviour
{
    public enum Kind { Road, Block, Park, Building }
    public Kind kind;
}
