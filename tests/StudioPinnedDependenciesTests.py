"""Exercise pinned worktree preparation against disposable local Git repositories."""
import json
import os
import pathlib
import subprocess
import tempfile
import unittest

REPOSITORY = pathlib.Path(__file__).parents[1]
VERIFIER = pathlib.Path(os.environ.get('STUDIO_SECURITY_BASELINE_VERIFIER', str(REPOSITORY.parent / 'Common/Common.Security/scripts/verify-source-baseline.py')))


class StudioPinnedDependenciesTests(unittest.TestCase):
    def test_merged_pins_are_used_without_switching_shared_checkouts_and_unmerged_pins_fail(self):
        with tempfile.TemporaryDirectory(prefix='studio-pin-test-') as name:
            root = pathlib.Path(name)
            studio = root / 'Aegis.Studio'
            studio.mkdir()
            (studio / 'Aegis.Studio.slnx').write_text('<Solution><Project Path="Studio.csproj"/><Project Path="../Common/Common.Security/Common.Security.csproj"/></Solution>')
            pins, heads = {}, {}
            libraries = ['Common.Diagnostics', 'Common.Messaging', 'Common.Registration', 'Common.Secrets', 'Common.Security', 'Common.Storage']
            for library in libraries:
                path = root / 'Common' / library
                path.mkdir(parents=True)
                def git(*args):
                    return subprocess.check_output(['git', '-C', str(path), *args], text=True, stderr=subprocess.DEVNULL).strip()
                git('init', '-b', 'main')
                (path / 'source.txt').write_text('reviewed source')
                if library == 'Common.Security':
                    (path / 'scripts').mkdir()
                    (path / 'scripts/verify-source-baseline.py').write_text(VERIFIER.read_text())
                git('add', '.')
                git('-c', 'user.name=Local test', '-c', 'user.email=local@example.test', 'commit', '-m', 'reviewed source')
                pins[library] = git('rev-parse', 'HEAD')
                (path / 'source.txt').write_text('later main source')
                git('add', '.')
                git('-c', 'user.name=Local test', '-c', 'user.email=local@example.test', 'commit', '-m', 'later main')
                heads[library] = git('rev-parse', 'HEAD')
            manifest = studio / 'security-baseline.json'
            manifest.write_text(json.dumps({'commonLibraries': pins}))
            driver = root / 'verify.ps1'
            # No real deployments or network access; only generated Git worktrees.
            driver.write_text('''param($Helper,$Root,$Studio,$Output)
$ErrorActionPreference = "Stop"
. $Helper
$d = New-StudioPinnedDependencies -ProjectsRoot $Root -StudioPath $Studio
try {
    $heads = @{}
    foreach ($w in $d.Worktrees) {
        $heads[[IO.Path]::GetFileName($w.Path)] = (& git -C $w.Path rev-parse HEAD).Trim()
    }
    @{Root=$d.CommonRoot; Heads=$heads; Solution=(Get-Content $d.Solution -Raw)} | ConvertTo-Json -Depth 5 | Set-Content $Output
} finally { Remove-StudioPinnedDependencies -Dependencies $d }
''')
            output = root / 'result.json'
            args = ['pwsh', '-NoProfile', '-File', str(driver), str(REPOSITORY / 'scripts/Studio-PinnedDependencies.ps1'), str(root), str(studio), str(output)]
            result = subprocess.run(args, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            report = json.loads(output.read_text())
            self.assertEqual(pins, report['Heads'])
            self.assertIn(str(studio / 'Studio.csproj'), report['Solution'])
            self.assertIn(str(pathlib.Path(report['Root']) / 'Common.Security/Common.Security.csproj'), report['Solution'])
            self.assertFalse(pathlib.Path(report['Root']).exists())
            for library, expected in heads.items():
                path = root / 'Common' / library
                self.assertEqual(expected, subprocess.check_output(['git', '-C', str(path), 'rev-parse', 'HEAD'], text=True).strip())
                self.assertEqual('main', subprocess.check_output(['git', '-C', str(path), 'branch', '--show-current'], text=True).strip())
                self.assertEqual('', subprocess.check_output(['git', '-C', str(path), 'status', '--porcelain'], text=True).strip())
            pins['Common.Security'] = 'f' * 40
            manifest.write_text(json.dumps({'commonLibraries': pins}))
            rejected = subprocess.run(args, capture_output=True, text=True)
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn('not merged in authoritative main', rejected.stderr)


if __name__ == '__main__':
    unittest.main()
