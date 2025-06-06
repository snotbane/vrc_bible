﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BibleDeedListener : UdonSharpBehaviour
{
    [SerializeField] public BibleDeed deed;

    protected virtual void Start()
    {
        Refresh();
    }

    protected virtual void Update()
    {
        if (deed == null) return;

        Refresh();
    }

    public override void OnDeserialization()
    {
        Refresh();
    }

    public virtual void Sync()
    {
        Refresh();
        RequestSerialization();
    }

    public virtual void Refresh() { }
}
