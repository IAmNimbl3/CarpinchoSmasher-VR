using UnityEngine;

public class CarpinchoVelocista : Enemy
{
    public override CarpinchoType Type => CarpinchoType.Velocista;

    // IA pendiente (próxima pasada): chase con NavMesh, telegraph del ataque melee,
    // estado de vulnerabilidad post-esquive (ventana en la que se puede matar con golpe directo).
}
