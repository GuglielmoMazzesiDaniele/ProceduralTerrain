using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Movement")]
    public float lookSpeed = 2.0f;  
    public float moveSpeed = 10.0f; 
    public float zoomSpeed = 5.0f;  
    public float panSpeed = 0.5f;
    public float turboMultiplier = 10.0f;

    private Vector3 _rotation;

    #region Unity Callback Functions

    private void Start()
    {
        _rotation = transform.eulerAngles;
    }

    private void Update()
    {
        HandleMouseLook();
        HandleMousePan();
        HandleZoom();
        HandleKeyboardMovement();
    }

    #endregion

    #region Handlers

    /// <summary>
    /// If the right button of the mouse is pressed, change the camera rotation
    /// </summary>
    private void HandleMouseLook()
    {
        // If right button is not pressed, returned
        if (!Input.GetMouseButton(1))
            return;
        
        // Computing the rotation of the current frame
        var mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        var mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        // Applying the delta rotation to the stored rotation
        _rotation.x -= mouseY;
        _rotation.y += mouseX;
        
        // Setting the rotation on the transform
        transform.eulerAngles = _rotation;
        
    }

    /// <summary>
    /// If the middle mouse is pressed, change the camera position
    /// </summary>
    private void HandleMousePan()
    {
        // If middle button is not pressed, returned
        if (!Input.GetMouseButton(2))
            return;
        
        // Computing the translation of the current frame
        var panX = -Input.GetAxis("Mouse X") * panSpeed;
        var panY = -Input.GetAxis("Mouse Y") * panSpeed;

        // Applying the translation
        transform.position += transform.right * panX + transform.up * panY;
    }

    /// <summary>
    /// On mouse wheel scrolled, zoom in or out
    /// </summary>
    private void HandleZoom()
    {
        // Verifies if the wheel has been scrolled
        var scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        
        // Translate the camera by the zoom
        transform.position += transform.forward * scroll;
    }

    /// <summary>
    /// On WASDQE keys press, translate the camera accordingly
    /// </summary>
    private void HandleKeyboardMovement()
    {
        // Initializing the current translation
        var currentTranslation = new Vector3();
        
        // Verifying if any of the hotkeys is pressed and changing the current translation accordingly
        if (Input.GetKey(KeyCode.W)) currentTranslation += transform.forward;
        if (Input.GetKey(KeyCode.S)) currentTranslation -= transform.forward;
        if (Input.GetKey(KeyCode.A)) currentTranslation -= transform.right;
        if (Input.GetKey(KeyCode.D)) currentTranslation += transform.right;
        if (Input.GetKey(KeyCode.Q)) currentTranslation -= transform.up;
        if (Input.GetKey(KeyCode.E)) currentTranslation += transform.up;
        
        // Verifying if the shift button is pressed
        if (Input.GetKeyDown(KeyCode.LeftShift)) moveSpeed *= turboMultiplier;
        if (Input.GetKeyUp(KeyCode.LeftShift)) moveSpeed /= turboMultiplier;

        // Applying the translation
        transform.position += moveSpeed * Time.deltaTime * currentTranslation;
    }

    #endregion
}