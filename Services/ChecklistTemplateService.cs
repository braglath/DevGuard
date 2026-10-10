using System.Collections.Generic;
using DevGuard.Models;

namespace DevGuard.Services;

public class ChecklistTemplateService
{
   public List<ChecklistItem> GetTemplate(WorkType workType)
   {
      return workType switch
      {
         WorkType.Bug => BugChecklist(),
         WorkType.Feature => FeatureChecklist(),
         WorkType.Development => DevelopmentChecklist(),
         WorkType.Testing => TestingChecklist(),
         WorkType.PullRequest => PullRequestChecklist(),
         WorkType.Blank =>  new List<ChecklistItem>(),
         _ => new List<ChecklistItem>()
      };
   }

   private static List<ChecklistItem> BugChecklist() =>
   [
       Item("Understand the exact issue and expected behaviour", "Understand", ChecklistPriority.Critical),
        Item("Reproduce the issue", "Understand", ChecklistPriority.Critical),
        Item("Understand the complete affected flow", "Understand", ChecklistPriority.Critical),
        Item("Identify the root cause", "Understand", ChecklistPriority.Critical),
        Item("Identify impacted modules and dependencies", "Understand", ChecklistPriority.Required),
        Item("Confirm assumptions with concerned teammates", "Understand", ChecklistPriority.Required),

        Item("Implement only the required fix", "Development", ChecklistPriority.Required),
        Item("Remove temporary/debug/hardcoded logic", "Development", ChecklistPriority.Critical),
        Item("Self-review the complete code diff", "Development", ChecklistPriority.Required),

        Item("Test the original failure scenario", "Testing", ChecklistPriority.Critical),
        Item("Test the expected successful scenario", "Testing", ChecklistPriority.Required),
        Item("Test regression and edge cases", "Testing", ChecklistPriority.Required),

        Item("Inform affected teammates", "Communication", ChecklistPriority.Required),

        Item("Confirm PR approval and checks", "PR", ChecklistPriority.Required),
        Item("Merge PR and verify the merge completed", "Merge", ChecklistPriority.Critical),

        Item("Verify deployed build/version", "Deployment", ChecklistPriority.Required),
        Item("Complete post-deployment smoke test", "Deployment", ChecklistPriority.Required),
        Item("Communicate completion", "Closure", ChecklistPriority.Required)
   ];

   private static List<ChecklistItem> FeatureChecklist() =>
   [
       Item("Understand the complete requirement", "Understand", ChecklistPriority.Critical),
        Item("Understand the complete user flow", "Understand", ChecklistPriority.Critical),
        Item("Identify impacted existing flows", "Understand", ChecklistPriority.Required),
        Item("Identify dependencies and affected modules", "Understand", ChecklistPriority.Required),
        Item("Confirm assumptions and unclear requirements", "Understand", ChecklistPriority.Critical),

        Item("Agree on implementation approach", "Development", ChecklistPriority.Required),
        Item("Handle loading, success, empty and error states", "Development", ChecklistPriority.Required),
        Item("Remove temporary/debug/hardcoded logic", "Development", ChecklistPriority.Critical),
        Item("Self-review the complete code diff", "Development", ChecklistPriority.Required),

        Item("Test happy path", "Testing", ChecklistPriority.Required),
        Item("Test negative and edge cases", "Testing", ChecklistPriority.Required),
        Item("Test affected existing functionality", "Testing", ChecklistPriority.Required),

        Item("Inform affected teammates", "Communication", ChecklistPriority.Required),

        Item("Confirm PR approval and checks", "PR", ChecklistPriority.Required),
        Item("Merge PR and verify the merge completed", "Merge", ChecklistPriority.Critical),

        Item("Verify build/version and deployment", "Deployment", ChecklistPriority.Required),
        Item("Complete smoke test", "Deployment", ChecklistPriority.Required),
        Item("Communicate completion", "Closure", ChecklistPriority.Required)
   ];

   private static List<ChecklistItem> DevelopmentChecklist() =>
   [
       Item("Understand the requirement", "Understand", ChecklistPriority.Critical),
        Item("Understand the complete flow", "Understand", ChecklistPriority.Critical),
        Item("Identify dependencies and affected areas", "Understand", ChecklistPriority.Required),
        Item("Confirm assumptions with concerned teammates", "Understand", ChecklistPriority.Required),

        Item("Implement intended scope only", "Development", ChecklistPriority.Required),
        Item("No hardcoded/test-only logic remains", "Development", ChecklistPriority.Critical),
        Item("No temporary debugging code remains", "Development", ChecklistPriority.Critical),
        Item("Review complete code diff", "Development", ChecklistPriority.Critical),

        Item("Build succeeds", "Testing", ChecklistPriority.Required),
        Item("Test changed functionality", "Testing", ChecklistPriority.Required),
        Item("Test regression scenarios", "Testing", ChecklistPriority.Required),

        Item("Communicate changes/dependencies", "Communication", ChecklistPriority.Required),

        Item("Create PR against correct target branch", "PR", ChecklistPriority.Required)
   ];

   private static List<ChecklistItem> TestingChecklist() =>
   [
       Item("Understand expected behaviour", "Understand", ChecklistPriority.Critical),
        Item("Identify primary test scenario", "Planning", ChecklistPriority.Required),
        Item("Test happy path", "Testing", ChecklistPriority.Required),
        Item("Test negative/error scenarios", "Testing", ChecklistPriority.Required),
        Item("Test edge cases", "Testing", ChecklistPriority.Required),
        Item("Test regression scenarios", "Testing", ChecklistPriority.Required),
        Item("Verify affected existing functionality", "Testing", ChecklistPriority.Required),
        Item("Verify correct environment/configuration", "Testing", ChecklistPriority.Required),
        Item("Record and communicate discovered issues", "Communication", ChecklistPriority.Required),
        Item("Confirm testing is complete", "Closure", ChecklistPriority.Critical)
   ];

   private static List<ChecklistItem> PullRequestChecklist() =>
   [
       Item("Confirm PR title and description", "PR", ChecklistPriority.Required),
        Item("Confirm correct target branch", "PR", ChecklistPriority.Critical),
        Item("Confirm correct reviewers", "PR", ChecklistPriority.Required),
        Item("Review entire diff yourself", "PR", ChecklistPriority.Critical),
        Item("Confirm no unrelated changes", "PR", ChecklistPriority.Required),
        Item("Confirm no hardcoded/debug logic", "PR", ChecklistPriority.Critical),
        Item("Confirm CI/build checks pass", "PR", ChecklistPriority.Required),
        Item("Address all review comments", "PR", ChecklistPriority.Required),
        Item("Confirm PR approval", "PR", ChecklistPriority.Critical),
        Item("Merge PR", "Merge", ChecklistPriority.Critical),
        Item("Verify merge completed", "Merge", ChecklistPriority.Critical),
        Item("Verify correct build/version", "Deployment", ChecklistPriority.Required),
        Item("Verify deployment", "Deployment", ChecklistPriority.Required),
        Item("Complete smoke test", "Deployment", ChecklistPriority.Required),
        Item("Inform concerned teammates", "Communication", ChecklistPriority.Required)
   ];

   private static ChecklistItem Item(
       string title,
       string category,
       ChecklistPriority priority)
   {
      return new ChecklistItem
      {
         Title = title,
         Category = category,
         Priority = priority
      };
   }
}