using UnityEngine;

namespace ROC.Game.Sessions
{
    [DisallowMultipleComponent]
    public sealed class ServerRepositoryProvider : MonoBehaviour
    {
        public static ServerRepositoryProvider Instance { get; private set; }

        [Header("Repositories")]
        [Tooltip("Assign LocalCharacterRepository for now. Later this can be replaced by a database-backed repository.")]
        [SerializeField] private MonoBehaviour characterRepositoryBehaviour;

        [Tooltip("Assign LocalCharacterRepository for now. Later this can be replaced by a database-backed location repository.")]
        [SerializeField] private MonoBehaviour playerLocationRepositoryBehaviour;

        private ICharacterRepository _characterRepository;
        private IPlayerLocationRepository _playerLocationRepository;

        public ICharacterRepository CharacterRepository
        {
            get
            {
                ResolveIfNeeded();
                return _characterRepository;
            }
        }

        public IPlayerLocationRepository PlayerLocationRepository
        {
            get
            {
                ResolveIfNeeded();
                return _playerLocationRepository;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveIfNeeded();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void ResolveIfNeeded()
        {
            if (_characterRepository == null)
            {
                if (characterRepositoryBehaviour is ICharacterRepository characterRepository)
                {
                    _characterRepository = characterRepository;
                }
                else if (LocalCharacterRepository.Instance != null)
                {
                    _characterRepository = LocalCharacterRepository.Instance;
                }
            }

            if (_playerLocationRepository == null)
            {
                if (playerLocationRepositoryBehaviour is IPlayerLocationRepository locationRepository)
                {
                    _playerLocationRepository = locationRepository;
                }
                else if (LocalCharacterRepository.Instance != null)
                {
                    _playerLocationRepository = LocalCharacterRepository.Instance;
                }
            }
        }

        public bool HasRequiredRepositories()
        {
            ResolveIfNeeded();
            return _characterRepository != null && _playerLocationRepository != null;
        }
    }
}