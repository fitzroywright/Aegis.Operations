"""Execute the config-only dispatch with a harmless recording helper, never real sudo/systemd."""
import pathlib
import subprocess
import tempfile
import unittest


class RequestPortalDirectoryCommissioningTests(unittest.TestCase):
    def dispatch(self, mode="0:755", extra=False):
        source = (pathlib.Path(__file__).parents[1] / "scripts/Deploy-Kratos-Server.sh").read_text()
        decision = source.split('SOURCE_ROOT=', 1)[0]
        with tempfile.TemporaryDirectory() as temporary:
            helper = pathlib.Path(temporary) / "recording-helper"
            helper.write_text('#!/usr/bin/env bash\nprintf "helper:%s\\n" "$*"\n')
            helper.chmod(0o755)
            decision = decision.replace('/usr/local/sbin/aegis-requestportal-commission-directory', str(helper))
            harness = 'stat() { echo "' + mode + '"; }\n' + decision + '\necho deployment-path-reached\n'
            return subprocess.run(["bash", "-c", harness, "scope-test", "--requestportal-directory-commission"] + (["unexpected"] if extra else []), capture_output=True, text=True)

    def test_config_only_calls_exact_fixed_helper_and_exits_before_deployment(self):
        result = self.dispatch()
        self.assertEqual(0, result.returncode)
        self.assertEqual("helper:--apply\n", result.stdout)

    def test_writable_or_non_root_helper_is_never_executed(self):
        for mode in ("0:777", "1000:755"):
            result = self.dispatch(mode)
            self.assertNotEqual(0, result.returncode)
            self.assertNotIn("helper:", result.stdout)
            self.assertNotIn("deployment-path-reached", result.stdout)

    def test_extra_arguments_cannot_enter_config_only_dispatch(self):
        result = self.dispatch(extra=True)
        self.assertNotIn("helper:", result.stdout)


if __name__ == "__main__":
    unittest.main()
