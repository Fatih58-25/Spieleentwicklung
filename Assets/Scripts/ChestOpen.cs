using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;

public class ChestOpen : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject promptCanvas;         
    public TextMeshProUGUI promptText;      
    public CanvasGroup canvasGroup;         

    [Header("Animation Settings")]
    public Animator chestAnimator;          

    [Header("Collider Settings")]
    public Collider interactionCollider;    

    [Header("Item & Player Settings")]
    public GameObject swordObject;          // Sandığın içindeki kılıç
    public Transform playerHand;            // Oyuncunun elini temsil eden boş GameObject

    [Header("Timing Settings")]
    public float itemPromptDelay = 1.5f; 

    private bool playerInZone = false; 
    private bool isOpened = false;     
    private bool animationFinished = false; 
    private bool itemTaken = false;         

    void Start()
    {
        if (canvasGroup == null && promptCanvas != null)
        {
            canvasGroup = promptCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = promptCanvas.AddComponent<CanvasGroup>();
            }
        }

        HidePrompt();

        if (chestAnimator == null)
            chestAnimator = GetComponent<Animator>();

        if (interactionCollider == null)
            interactionCollider = GetComponent<Collider>();

        // Kılıç başta sandığın içinde görünür olsun
        if (swordObject != null)
            swordObject.SetActive(true);
    }

    void Update()
    {
        if (!playerInZone) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (!isOpened)
            {
                OpenChest(); 
            }
            else if (animationFinished && !itemTaken)
            {
                TakeItem();  
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = true;
            UpdatePromptUI();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInZone = false;
            HidePrompt();
        }
    }

    void OpenChest()
    {
        isOpened = true;

        if (chestAnimator != null)
        {
            chestAnimator.SetBool("IsOpen", true);
        }

        HidePrompt();

        StartCoroutine(WaitForAnimationRoutine());
    }

    IEnumerator WaitForAnimationRoutine()
    {
        yield return new WaitForSeconds(itemPromptDelay);

        animationFinished = true;

        if (playerInZone && !itemTaken)
        {
            if (promptText != null)
            {
                promptText.text = "[E] Gegenstand nehmen";
            }
            ShowPrompt();
        }
    }

    void TakeItem()
    {
        itemTaken = true;

        // KILICI OYUNCUNUN ELİNE IŞINLAMA VE BAĞLAMA KISMI
        if (swordObject != null && playerHand != null)
        {
            // 1. Kılıcın parent'ını oyuncunun eli yapıyoruz (artık oyuncuyla birlikte hareket edecek)
            swordObject.transform.SetParent(playerHand);

            // 2. Konum ve rotasyonu sıfırlayarak elin tam merkezine oturmasını sağlıyoruz
            swordObject.transform.localPosition = Vector3.zero;
            swordObject.transform.localRotation = Quaternion.identity;

            // 3. Eğer kılıçta fizik (Rigidbody) varsa kapatalım ki havada uçmasın veya oyuncuyu ittirmesin
            Rigidbody rb = swordObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            // Eğer kılıcın kendi collider'ı varsa (yerde etkileşime girmesin diye) kapatabilirsin:
            Collider swordCol = swordObject.GetComponent<Collider>();
            if (swordCol != null)
            {
                swordCol.enabled = false;
            }
        }

        HidePrompt();

        if (interactionCollider != null)
        {
            interactionCollider.enabled = false;
        }
    }

    void ShowPrompt()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    void HidePrompt()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void UpdatePromptUI()
    {
        if (canvasGroup == null) return;

        if (!isOpened)
        {
            if (promptText != null) promptText.text = "[E] Öffnen";
            ShowPrompt();
        }
        else if (animationFinished && !itemTaken)
        {
            if (promptText != null) promptText.text = "[E] Gegenstand nehmen";
            ShowPrompt();
        }
    }
}