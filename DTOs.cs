using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AzureDevOpsPRBot;

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

    public class CommitResponse
    {
        public int Count { get; set; }
        public List<Commit> Value { get; set; }
    }

    public class Commit
    {
        public string? CommitId { get; set; }
    }
}