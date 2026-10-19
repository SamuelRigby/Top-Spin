using UnityEngine;

public class Repairable : MonoBehaviour
{
    private bool _playerInRange = false;

    private void Awake()
    {
        Objectives.ObjectivesCount++;
    }

    private void OnEnable()
    {
        PlayerRepair.InteractEvent += Interacted;
    }

    private void OnDisable()
    {
        PlayerRepair.InteractEvent -= Interacted;
    }

    private void Interacted()
    {
        if (_playerInRange)
        {
            Objectives.ObjectivesCount--;
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
        }
    }
}
