"""Execute the deployer's migration decision with harmless recording substitutes."""
import pathlib
import subprocess
import unittest


class StudioDeploymentScopeTests(unittest.TestCase):
    def decision(self, *applications):
        source = (pathlib.Path(__file__).parents[1] / "scripts/Deploy-Kratos-Server.sh").read_text()
        decision = source.split("trap 'rc=$?; if [[ $rc -ne 0 ]]; then rollback; fi; exit $rc' EXIT", 1)[1].split('log "Stopping selected services"', 1)[0]
        harness = """
set -euo pipefail
SELECTED=("$@")
is_selected() { local item; for item in "${SELECTED[@]}"; do [[ "$item" != "$1" ]] || return 0; done; return 1; }
log() { :; }
migrate_common_secrets_env() { echo mutation:secrets; }
cleanup_legacy_registration() { echo mutation:registration; }
"""
        return subprocess.run(["bash", "-c", harness + decision, "scope-test", *applications], check=True, capture_output=True, text=True).stdout

    def test_studio_only_preserves_all_protected_configuration_and_control_plane_state(self):
        self.assertEqual("", self.decision("studio"))

    def test_public_site_only_preserves_its_existing_isolation(self):
        self.assertEqual("", self.decision("publicsite"))

    def test_requestportal_only_preserves_secrets_and_other_application_registration(self):
        self.assertEqual("", self.decision("requestportal"))

    def test_control_plane_rollout_retains_its_explicit_migrations(self):
        self.assertEqual("mutation:secrets\nmutation:registration\n", self.decision("operations", "configuration", "diagnostics"))

    def test_studio_does_not_create_or_chmod_other_services_state(self):
        source = (pathlib.Path(__file__).parents[1] / "scripts/Deploy-Kratos-Server.sh").read_text()
        decision = source.split('log "Ensuring persistent state only for selected applications"', 1)[1].split('if is_selected hello;', 1)[0]
        harness = '''set -euo pipefail
SELECTED=(studio)
OWNER=aegis:aegis
mkdir() { echo mkdir:"$*"; }
chown() { echo chown:"$*"; }
chmod() { echo chmod:"$*"; }
'''
        result = subprocess.run(["bash", "-c", harness + decision], check=True, capture_output=True, text=True).stdout
        self.assertEqual(["mkdir:-p /var/lib/aegis/studio", "chown:-R aegis:aegis /var/lib/aegis/studio", "chmod:700 /var/lib/aegis/studio"], result.splitlines())


if __name__ == "__main__":
    unittest.main()
