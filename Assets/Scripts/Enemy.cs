using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private GameObject vfx;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Weapon"))
        {
            Instantiate(vfx, transform.position, Quaternion.identity);
            Destroy(collision.gameObject);
            Destroy(gameObject);
        }
    }
}
