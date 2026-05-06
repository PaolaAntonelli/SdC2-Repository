// ============================================================
// File: Arch System/ArchInteraction.cs
// Descrizione: Gestisce l'interazione utente con l'arco:
//              - Aggiunta/rimozione carichi e vincoli
//              - Manipolazione con mouse/VR
//              - Input da tastiera
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using ArchSystem;

namespace ArchSystem
{
    [RequireComponent(typeof(ArchController))]
    public class ArchInteraction : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject loadPrefab;         // Prefab per visualizzare i carichi
        public GameObject supportPrefab;      // Prefab per visualizzare i vincoli
        
        [Header("Interazione Mouse")]
        public bool enableMouseInteraction = true;
        public float mouseClickRadius = 0.5f;
        public LayerMask interactionLayer;
        
        [Header("Vincoli Interazione")]
        public float minDistanceBetweenElements = 0.3f;
        
        [Header("Input Keys")]
        public KeyCode addLoadKey = KeyCode.L;
        public KeyCode addSupportKey = KeyCode.K;
        public KeyCode removeElementKey = KeyCode.R;
        public KeyCode solveKey = KeyCode.Space;
        public KeyCode resetKey = KeyCode.Backspace;
        
        // Riferimenti interni
        private ArchController archController;
        private ArchVisualizer archVisualizer;
        private Camera mainCamera;
        
        // Stato interazione
        private List<GameObject> spawnedLoads = new List<GameObject>();
        private List<GameObject> spawnedSupports = new List<GameObject>();
        private bool isDragging = false;
        private GameObject draggedObject;
        private float dragInitialY;
        private float dragInitialZ;
        
        // Lista dei punti di interesse
        private List<Vector2> nodePositions;
        
        void Start()
        {
            archController = GetComponent<ArchController>();
            archVisualizer = GetComponent<ArchVisualizer>();
            mainCamera = Camera.main;
            
            // Aggiorna i riferimenti iniziali
            Invoke("UpdateNodePositions", 0.1f);
        }
        
        void Update()
        {
            HandleKeyboardInput();
            HandleMouseInput();
        }
        
        void HandleKeyboardInput()
        {
            // Aggiungi carico
            if (Input.GetKeyDown(addLoadKey))
            {
                Vector3 mouseWorldPos = GetMouseWorldPosition();
                if (mouseWorldPos != Vector3.zero)
                {
                    int nearestNode = FindNearestNode(mouseWorldPos);
                    if (nearestNode >= 0)
                    {
                        AddLoadAtNode(nearestNode);
                    }
                }
            }
            
            // Aggiungi vincolo
            if (Input.GetKeyDown(addSupportKey))
            {
                Vector3 mouseWorldPos = GetMouseWorldPosition();
                if (mouseWorldPos != Vector3.zero)
                {
                    int nearestNode = FindNearestNode(mouseWorldPos);
                    if (nearestNode >= 0)
                    {
                        AddSupportAtNode(nearestNode);
                    }
                }
            }
            
            // Rimuovi elemento
            if (Input.GetKeyDown(removeElementKey))
            {
                RemoveElementUnderMouse();
            }
            
            // Risolvi arco
            if (Input.GetKeyDown(solveKey))
            {
                archController.Solve();
                Debug.Log("[ArchInteraction] Arco risolto!");
            }
            
            // Reset
            if (Input.GetKeyDown(resetKey))
            {
                ResetArch();
            }
        }
        
        void HandleMouseInput()
        {
            if (!enableMouseInteraction) return;
            
            // Click sinistro per selezionare/trascinare
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                TrySelectObject();
            }
            
            // Rilascio
            if (Mouse.current?.leftButton.wasReleasedThisFrame == true)
            {
                if (isDragging && draggedObject != null)
                {
                    OnDragEnd();
                }
            }
            
            // Trascinamento
            if (isDragging && draggedObject != null)
            {
                DragObject();
            }
            
            // Click destro per rimuovere
            if (Mouse.current?.rightButton.wasPressedThisFrame == true)
            {
                RemoveElementUnderMouse();
            }
        }
        
        Vector3 GetMouseWorldPosition()
        {
            if (mainCamera == null || Mouse.current == null) return Vector3.zero;
            
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            
            // Piano di lavoro (supponiamo Z costante)
            Plane workPlane = new Plane(Vector3.forward, Vector3.zero);
            if (workPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
            
            return Vector3.zero;
        }
        
        int FindNearestNode(Vector3 worldPosition)
        {
            UpdateNodePositions();
            if (nodePositions == null || nodePositions.Count == 0) return -1;
            
            Vector2 clickPoint = new Vector2(worldPosition.x, worldPosition.y);
            int nearestNode = -1;
            float minDistance = float.MaxValue;
            
            for (int i = 0; i < nodePositions.Count; i++)
            {
                float dist = Vector2.Distance(clickPoint, nodePositions[i]);
                if (dist < minDistance && dist < mouseClickRadius)
                {
                    minDistance = dist;
                    nearestNode = i;
                }
            }
            
            return nearestNode;
        }
        
        void TrySelectObject()
        {
            Vector3 mousePos = GetMouseWorldPosition();
            if (mousePos == Vector3.zero) return;
            
            GameObject closest = FindClosestInteractiveObject(mousePos);
            if (closest != null)
            {
                draggedObject = closest;
                isDragging = true;
                dragInitialY = closest.transform.position.y;
                dragInitialZ = closest.transform.position.z;
                
                Debug.Log($"[ArchInteraction] Oggetto selezionato: {closest.name}");
            }
        }
        
        GameObject FindClosestInteractiveObject(Vector3 position)
        {
            GameObject closest = null;
            float minDist = mouseClickRadius;
            
            // Cerca tra i carichi
            foreach (var obj in spawnedLoads)
            {
                if (obj == null) continue;
                float dist = Vector2.Distance(
                    new Vector2(position.x, position.y),
                    new Vector2(obj.transform.position.x, obj.transform.position.y));
                
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = obj;
                }
            }
            
            // Cerca tra i vincoli
            foreach (var obj in spawnedSupports)
            {
                if (obj == null) continue;
                float dist = Vector2.Distance(
                    new Vector2(position.x, position.y),
                    new Vector2(obj.transform.position.x, obj.transform.position.y));
                
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = obj;
                }
            }
            
            return closest;
        }
        
        void DragObject()
        {
            if (draggedObject == null) return;
            
            Vector3 mouseWorldPos = GetMouseWorldPosition();
            if (mouseWorldPos == Vector3.zero) return;
            
            int nearestNode = FindNearestNode(mouseWorldPos);
            if (nearestNode >= 0)
            {
                // Verifica che non ci siano altri oggetti troppo vicini
                if (!IsPositionOccupied(nearestNode, draggedObject))
                {
                    Vector3 newPos = new Vector3(
                        nodePositions[nearestNode].x,
                        nodePositions[nearestNode].y,
                        dragInitialZ
                    );
                    
                    // Mantieni l'offset verticale per distinguere carichi e vincoli
                    if (draggedObject.CompareTag("Load"))
                        newPos.y += 0.3f;
                    else if (draggedObject.CompareTag("Support"))
                        newPos.y -= 0.3f;
                    
                    draggedObject.transform.position = newPos;
                    
                    // Aggiorna l'indice del nodo nell'oggetto (se ha un componente)
                    var nodeRef = draggedObject.GetComponent<ArchNodeReference>();
                    if (nodeRef != null)
                        nodeRef.nodeIndex = nearestNode;
                }
            }
        }
        
        void OnDragEnd()
        {
            if (draggedObject != null)
            {
                // Aggiorna le liste nell'ArchController
                UpdateArchControllerLists();
                
                Debug.Log($"[ArchInteraction] Oggetto rilasciato: {draggedObject.name}");
            }
            
            isDragging = false;
            draggedObject = null;
        }
        
        bool IsPositionOccupied(int nodeIndex, GameObject excludeObject)
        {
            Vector2 nodePos = nodePositions[nodeIndex];
            
            // Controlla carichi
            foreach (var obj in spawnedLoads)
            {
                if (obj == null || obj == excludeObject) continue;
                Vector2 objPos = new Vector2(obj.transform.position.x, obj.transform.position.y);
                if (Vector2.Distance(nodePos, objPos) < minDistanceBetweenElements)
                    return true;
            }
            
            // Controlla vincoli
            foreach (var obj in spawnedSupports)
            {
                if (obj == null || obj == excludeObject) continue;
                Vector2 objPos = new Vector2(obj.transform.position.x, obj.transform.position.y);
                if (Vector2.Distance(nodePos, objPos) < minDistanceBetweenElements)
                    return true;
            }
            
            return false;
        }
        
        void AddLoadAtNode(int nodeIndex)
        {
            if (loadPrefab == null) return;
            if (IsPositionOccupied(nodeIndex, null)) return;
            
            Vector3 spawnPos = new Vector3(
                nodePositions[nodeIndex].x,
                nodePositions[nodeIndex].y + 0.3f,
                transform.position.z
            );
            
            GameObject loadObj = Instantiate(loadPrefab, spawnPos, Quaternion.identity);
            loadObj.tag = "Load";
            
            // Aggiungi riferimento al nodo
            var nodeRef = loadObj.AddComponent<ArchNodeReference>();
            nodeRef.nodeIndex = nodeIndex;
            
            spawnedLoads.Add(loadObj);
            UpdateArchControllerLists();
            
            Debug.Log($"[ArchInteraction] Carico aggiunto al nodo {nodeIndex}");
        }
        
        void AddSupportAtNode(int nodeIndex)
        {
            if (supportPrefab == null) return;
            if (IsPositionOccupied(nodeIndex, null)) return;
            
            Vector3 spawnPos = new Vector3(
                nodePositions[nodeIndex].x,
                nodePositions[nodeIndex].y - 0.3f,
                transform.position.z
            );
            
            GameObject supportObj = Instantiate(supportPrefab, spawnPos, Quaternion.identity);
            supportObj.tag = "Support";
            
            // Aggiungi riferimento al nodo
            var nodeRef = supportObj.AddComponent<ArchNodeReference>();
            nodeRef.nodeIndex = nodeIndex;
            
            spawnedSupports.Add(supportObj);
            UpdateArchControllerLists();
            
            Debug.Log($"[ArchInteraction] Vincolo aggiunto al nodo {nodeIndex}");
        }
        
        void RemoveElementUnderMouse()
        {
            Vector3 mousePos = GetMouseWorldPosition();
            if (mousePos == Vector3.zero) return;
            
            GameObject toRemove = FindClosestInteractiveObject(mousePos);
            if (toRemove != null)
            {
                RemoveElement(toRemove);
            }
        }
        
        void RemoveElement(GameObject obj)
        {
            if (obj.CompareTag("Load"))
            {
                spawnedLoads.Remove(obj);
            }
            else if (obj.CompareTag("Support"))
            {
                spawnedSupports.Remove(obj);
            }
            
            Destroy(obj);
            UpdateArchControllerLists();
            
            Debug.Log($"[ArchInteraction] Elemento rimosso: {obj.name}");
        }
        
        void UpdateArchControllerLists()
        {
            // Aggiorna le liste nell'ArchController
            var loadIndicesField = typeof(ArchController).GetField("loadNodeIndices",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var supportIndicesField = typeof(ArchController).GetField("supportNodeIndices",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (loadIndicesField != null)
            {
                List<int> loadIndices = new List<int>();
                foreach (var obj in spawnedLoads)
                {
                    if (obj == null) continue;
                    var nodeRef = obj.GetComponent<ArchNodeReference>();
                    if (nodeRef != null)
                        loadIndices.Add(nodeRef.nodeIndex);
                }
                loadIndicesField.SetValue(archController, loadIndices);
            }
            
            if (supportIndicesField != null)
            {
                List<int> supportIndices = new List<int>();
                foreach (var obj in spawnedSupports)
                {
                    if (obj == null) continue;
                    var nodeRef = obj.GetComponent<ArchNodeReference>();
                    if (nodeRef != null)
                        supportIndices.Add(nodeRef.nodeIndex);
                }
                supportIndicesField.SetValue(archController, supportIndices);
            }
        }
        
        void UpdateNodePositions()
        {
            var field = typeof(ArchController).GetField("nodePositions",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            nodePositions = field?.GetValue(archController) as List<Vector2>;
        }
        
        void ResetArch()
        {
            // Rimuovi tutti gli elementi spawnati
            foreach (var obj in spawnedLoads)
                if (obj != null) Destroy(obj);
            foreach (var obj in spawnedSupports)
                if (obj != null) Destroy(obj);
            
            spawnedLoads.Clear();
            spawnedSupports.Clear();
            
            // Reimposta i default
            archController.Invoke("SetupDefaultBoundaries", 0f);
        }
        
        void OnDrawGizmos()
        {
            // Visualizza i punti di interazione
            if (nodePositions == null) return;
            
            Gizmos.color = Color.yellow;
            foreach (var point in nodePositions)
            {
                Vector3 worldPoint = new Vector3(point.x, point.y, transform.position.z);
                Gizmos.DrawWireSphere(worldPoint, 0.1f);
            }
        }
    }
    
    // Componente helper per tenere traccia dell'indice del nodo
    public class ArchNodeReference : MonoBehaviour
    {
        public int nodeIndex;
    }
}