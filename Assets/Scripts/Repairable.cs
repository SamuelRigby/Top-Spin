using UnityEngine;

public class Repairable : MonoBehaviour
{
    [SerializeField] GameObject _textPopupPrefab;
    [SerializeField] float _textYOffset;
    private GameObject _interactPrompt;
    private bool _playerInRange = false;

    void Start()
    {
        Objectives.Instance.ObjectivesCount++;
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
            Objectives.Instance.ObjectivesCount--;
            Objectives.Instance.WinCheck();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _interactPrompt = Instantiate(_textPopupPrefab, transform.position + (_textYOffset * transform.up), Quaternion.identity);
            _interactPrompt.transform.SetParent(transform, true);
            _interactPrompt.GetComponent<WorldTextPopup>().Init("[E]", 72, Color.white);
            _playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(_interactPrompt);
            _interactPrompt = null;
            _playerInRange = false;
        }
    }
}
