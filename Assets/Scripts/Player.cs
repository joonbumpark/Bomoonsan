using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Mountains
{
    public class Player : MonoBehaviour
    {
        [HideInInspector] public GameManager GameManager;

        void Awake()
        {
            if (GameManager == null)
            {
                GameManager = FindObjectOfType<GameManager>();
            }
        }
    }
}
