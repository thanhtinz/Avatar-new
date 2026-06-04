// client/UnityClient/Scripts/Game/PlayerController.cs
using System.Collections;
using UnityEngine;
using FantasyWorld.Client.Network;
using FantasyWorld.Client.Game;
using FantasyWorld.Shared.Enums;

namespace FantasyWorld.Client.Game
{
    /// <summary>
    /// Controls local player movement, sends position to server
    /// Attach to player prefab. Uses click-to-move OR WASD.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed     = 5f;
        [SerializeField] private float sendInterval  = 0.1f;   // seconds between position updates
        [SerializeField] private bool  clickToMove   = true;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        private static readonly int IsMoving  = Animator.StringToHash("IsMoving");
        private static readonly int DirHash   = Animator.StringToHash("Direction"); // 0-7

        private CharacterController _cc;
        private Vector3             _targetPos;
        private bool                _isMoving;
        private float               _sendTimer;
        private MoveDir             _lastDir = MoveDir.Down;
        private Camera              _cam;

        // Portal interaction
        private int   _nearPortalId = -1;
        private bool  _showPortalHint;

        private void Awake()
        {
            _cc     = GetComponent<CharacterController>();
            _cam    = Camera.main;
            _targetPos = transform.position;
        }

        private void Update()
        {
            HandleInput();
            MoveToTarget();
            UpdateAnimation();
            HandlePortalInput();
            SendPositionTick();
        }

        // ─── Input ───────────────────────────────────────────

        private void HandleInput()
        {
            if (clickToMove)
            {
                HandleClickToMove();
            }
            else
            {
                HandleWASD();
            }
        }

        private void HandleClickToMove()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // Ignore UI clicks
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            var ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 100f, LayerMask.GetMask("Ground")))
                _targetPos = hit.point;
        }

        private void HandleWASD()
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return;

            var dir = new Vector3(h, 0, v).normalized;
            _targetPos = transform.position + dir * moveSpeed * Time.deltaTime * 10f;
        }

        private void MoveToTarget()
        {
            var dist = Vector3.Distance(transform.position, _targetPos);
            if (dist < 0.1f) { _isMoving = false; return; }

            var dir3 = (_targetPos - transform.position).normalized;
            _cc.Move(dir3 * moveSpeed * Time.deltaTime);
            _isMoving = true;

            // Face movement direction
            if (dir3.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(dir3);

            // Determine MoveDir enum
            _lastDir = CalcMoveDir(dir3);
        }

        private static MoveDir CalcMoveDir(Vector3 d)
        {
            var angle = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            return angle switch
            {
                > 157.5f or <= -157.5f => MoveDir.Down,
                > -157.5f and <= -112.5f => MoveDir.DownLeft,
                > -112.5f and <= -67.5f  => MoveDir.Left,
                > -67.5f  and <= -22.5f  => MoveDir.UpLeft,
                > -22.5f  and <=  22.5f  => MoveDir.Up,
                > 22.5f   and <=  67.5f  => MoveDir.UpRight,
                > 67.5f   and <= 112.5f  => MoveDir.Right,
                _                        => MoveDir.DownRight,
            };
        }

        private void UpdateAnimation()
        {
            if (animator == null) return;
            animator.SetBool(IsMoving, _isMoving);
            animator.SetInteger(DirHash, (int)_lastDir);
        }

        // ─── Portal ──────────────────────────────────────────

        private void HandlePortalInput()
        {
            if (_nearPortalId >= 0 && Input.GetKeyDown(KeyCode.F))
                _ = GameManager.Instance.UsePortalAsync(_nearPortalId);
        }

        private void OnTriggerEnter(Collider other)
        {
            var portal = other.GetComponent<PortalTrigger>();
            if (portal != null)
            {
                _nearPortalId = portal.PortalId;
                // Show "Press F to enter portal" hint
                UIHintManager.Show("portal", portal.PortalName);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<PortalTrigger>() != null)
            {
                _nearPortalId = -1;
                UIHintManager.Hide("portal");
            }
        }

        // ─── Send position ───────────────────────────────────

        private void SendPositionTick()
        {
            if (!_isMoving) return;

            _sendTimer += Time.deltaTime;
            if (_sendTimer < sendInterval) return;
            _sendTimer = 0;

            var p = transform.position;
            _ = GameManager.Instance.MoveAsync(p.x, p.z, _lastDir);
        }

        // ─── Public API ──────────────────────────────────────

        public void TeleportTo(Vector3 pos)
        {
            transform.position = pos;
            _targetPos = pos;
        }
    }

    // ─── Stubs ───────────────────────────────────────────────

    public class PortalTrigger : MonoBehaviour
    {
        public int    PortalId   { get; set; }
        public string PortalName { get; set; } = "";
    }

    public static class UIHintManager
    {
        public static void Show(string key, string text) { }
        public static void Hide(string key) { }
    }
}
