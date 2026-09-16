using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mountains
{
    public class Coin : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            other.GetComponent<Player>()?.AddCoin();
            GameObject.Destroy(gameObject);
        }
    }
}
