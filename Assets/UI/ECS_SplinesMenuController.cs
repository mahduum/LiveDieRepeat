using UnityEngine;
using UnityEngine.UIElements;

public class ECS_SplinesMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument _menuDocument;

    private VisualElement _root;
    private VisualElement _editButton;
    private VisualElement _saveButton;
    
    // Start is called before the first frame update
    private void Awake()
    {
        _root = _menuDocument.rootVisualElement;
    }

    private void OnEnable()
    {
        //todo try query
        UQueryBuilder<Button> query = _root.Query<Button>();
        query.ForEach(b => 
            {
                if (b.viewDataKey == "Edit")
                {
                    b.clicked += StartEditSpline;
                    // Debug.Log("Registering callback edit.");
                    // b.RegisterCallback<MouseDownEvent>(e =>
                    // {
                    //     StartEditSpline();
                    // });
                }
                else if (b.viewDataKey == "Save")
                {
                    b.clicked += SaveSpline;
                    // Debug.Log("Registering callback save.");
                    // b.RegisterCallback<MouseDownEvent>(e =>
                    // {
                    //     SaveSpline();
                    // });
                }
            }
        );
    }

    private void StartEditSpline()
    {
        Debug.Log("Start editing spline");
    }

    private void EndEditSpline()
    {
        
    }

    private void SaveSpline()
    {
        Debug.Log("Save edited spline");
    }
}
