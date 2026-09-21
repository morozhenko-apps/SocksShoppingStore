from pathlib import Path
import unittest

WORKFLOW = Path(".github/workflows/test-and-report.yml").read_text(encoding="utf-8")


class RunnerRoutingContractTest(unittest.TestCase):
    def test_pushes_are_owned_by_runner_router(self):
        header = WORKFLOW.split("jobs:", 1)[0]
        self.assertNotIn("\n  push:\n", header)
        self.assertIn("pull_request:\n    branches: [ main ]", header)
        self.assertIn("workflow_dispatch:\n    inputs:\n      runner:", header)

    def test_dev_lane_uses_router_input(self):
        self.assertIn(
            "fromJSON(inputs.runner == 'self-hosted' && "
            "'[\"self-hosted\",\"linux\",\"x64\",\"docker-builder\"]' || "
            "'[\"ubuntu-latest\"]')",
            WORKFLOW,
        )
        self.assertIn(
            "image: ${{ inputs.runner == 'self-hosted' && "
            "'mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim' || '' }}",
            WORKFLOW,
        )

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
