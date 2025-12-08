using System;
using System.Collections.Generic;

[Serializable]
public class MorphemeMetaData
{
    public string url;
    public string name;
    public float duration;
    public string exportedOn;
}

[Serializable]
public class MorphemeAttribute
{
    public string name;
}

[Serializable]
public class MorphemeItem
{
    public float start;
    public float end;
    public List<MorphemeAttribute> attributes;
}

[Serializable]
public class MorphemeRoot
{
    public MorphemeMetaData metaData;
    public List<MorphemeItem> data;
}