using System;
using UnityEngine;

public interface ILimbCollisionSource
{
    event Action<float, Vector3> OnLimbCollision;
}
