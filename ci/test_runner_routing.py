from pathlib import Path
import unittest

WORKFLOW = Path(".github/workflows/test-and-report.yml").read_text(encoding="utf-8")


class RunnerRoutingContractTest(unittest.TestCase):
    def test_pushes_are_owned_by_runner_router(self):
        header = WORKFLOW.split("jobs:", 1)[0]
        self.assertNotIn("\n  push:\n", header)
        self.assertIn("pull_request:\n    branches: [ main ]", header)
        self.assertIn("workflow_dispatch:\n    inputs:\n      runner:", header)

    def test_all_jobs_use_provider_hosted_runners(self):
        import re
        assigned = re.findall(r"^\\s+runs-on:\\s*(.+)$", WORKFLOW, flags=re.MULTILINE)
        self.assertEqual(["ubuntu-latest"] * 3, assigned)
        self.assertNotIn("runs-on: ${{", WORKFLOW)

    def test_router_input_never_allows_self_hosted_lane(self):
        header = WORKFLOW.split("jobs:", 1)[0]
        self.assertIn("default: hosted", header)
        self.assertIn("options:\\n          - hosted", header)
        self.assertNotIn("default: self-hosted", header)
        self.assertNotIn("          - self-hosted", header)

    def test_existing_deployment_semantics_are_preserved(self):
        self.assertIn("deploy-azure:", WORKFLOW)
        self.assertIn("azure/webapps-deploy", WORKFLOW)
        self.assertIn("manual-ui:", WORKFLOW)

    def test_local_selector_is_removed(self):
        self.assertFalse(Path(".github/workflows/select-runner.yml").exists())
        self.assertNotIn("RUNNER_STATUS_TOKEN", WORKFLOW)
        self.assertNotIn("needs.select-runner", WORKFLOW)


if __name__ == "__main__":
    unittest.main()
