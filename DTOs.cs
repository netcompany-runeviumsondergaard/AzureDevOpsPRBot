﻿namespace AzureDevOpsPRBot;

internal abstract partial class Program
{
    public class RefResponse
    {
        public int Count { get; set; }
        public List<Ref> Value { get; set; }
    }

    public class Ref
    {
        public string Name { get; set; }
        public string ObjectId { get; set; }
    }

    public class DiffResponse
    {
        public ChangeCount ChangeCounts { get; set; }
    }

    public class ChangeCount
    {
        public int Edit { get; set; }
        public int Add { get; set; }
        public int Delete { get; set; }
    }
}