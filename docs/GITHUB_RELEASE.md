[English](GITHUB_RELEASE.md) | [简体中文](zh-CN/GITHUB_RELEASE.md)

# Publishing GitHub previews

Installers are published to this repository's GitHub Releases; no remote application server is involved. Merge the PR first, then use the successful build artifact from `main`. Do not substitute a locally built installer for the CI artifact.

1. Fetch the latest full `origin/main` SHA and wait for that SHA's successful `build.yml` push/main run. CI verifies that DLL `ProductVersion` contains the checked-out SHA and completes core, layout, and isolated first/overwrite/uninstall checks.
2. Dispatch `release.yml` with `release_sha`, `build_run_id`, and an explicit authorization record in `approval_note`. It verifies latest-main and build provenance before checkout and again before publication.
3. Allow only the production iStart installer and its matching SHA256 file. Reject QA packages, extra files, and hash mismatches. Read the version from the project; the workflow supports preview versions only.
4. Create a draft, upload the installer/checksum/manifest, and publish it as a prerelease without marking it as the stable latest release. Download the public assets again to verify their hashes, tag commit, and `release-manifest.json`.
5. On failure, preserve the draft and evidence rather than publishing incomplete assets. Never overwrite an existing version or tag. Fix through a new PR and version. If acceptance fails after publication, retract public status and use a fix PR or explicitly approved rollback; do not replace assets under the same name.

The workflow uses GitHub Token `contents:write` / `actions:read`, the `github-release` environment, and serialized concurrency. No additional server secrets are needed. For the first release, PR #1 and v0.2.0-preview.13, the user explicitly authorized merging and publishing in conversation. At that time, main had no branch protection and no enforced environment approval had been verified. Release records preserve the actual authorization channel, protection state, and exact SHA without claiming GitHub enforced approval. Subsequent releases require explicit authorization and all checks; record conversation approval when that is the authorized channel.

Public data includes only the installer, SHA256 file, and `release-manifest.json` containing source SHA, build run, installer hash, and authorization evidence. Do not include favorites, runtime logs, history samples, QA packages, or local backups.

GitHub Actions temporary artifacts expire after 7 days. Retain public releases and verified rollback versions; do not automatically delete unknown releases or user configuration. The first release had no expired public versions to clean up. Prefer the previous verified installer for rollback, preserve user configuration first, and keep native-menu recovery available. Retracting a release does not delete configuration data.
