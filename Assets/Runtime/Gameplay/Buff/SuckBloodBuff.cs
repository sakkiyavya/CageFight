using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SuckBloodBuff : BuffBase
{
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        return true;
    }


    public override bool CancelBuff(GameObjectProperty prop)
    {
        return true;
    }
}
