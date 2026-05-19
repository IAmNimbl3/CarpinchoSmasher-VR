using UnityEngine;

public class CarpinchoJuggernaut : Enemy
{
    public override CarpinchoType Type => CarpinchoType.Juggernaut;

    // IA pendiente (próxima pasada): chase lento con NavMesh, escudo con zona vulnerable
    // randomizada (espalda/costado/etc), daño por contacto directo con el jugador.
}
