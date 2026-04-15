namespace WindroseServerManager.Forms;

partial class MainForm
{
    private async Task CreateBackupNowAsync()
    {
        AppendConsole("[BACKUP] Creating backup...", Color.FromArgb(156, 110, 201));
        string? result = await _backupService.CreateBackupAsync();

        if (result != null)
        {
            AppendConsole($"[BACKUP] Saved: {Path.GetFileName(result)}", ThemeManager.StateRunning);
            RefreshBackupList();
        }
        else
        {
            AppendConsole("[BACKUP] Backup failed. Check the log for details.", ThemeManager.StateCrashed);
        }
    }

    private async Task RestoreBackupAsync()
    {
        if (dgvBackups.SelectedRows.Count == 0)
        {
            MessageBox.Show("Select a backup to restore.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? backupPath = dgvBackups.SelectedRows[0].Tag as string;
        if (backupPath == null) return;

        if (_serverManager.IsRunning)
        {
            MessageBox.Show("Stop the server before restoring a backup.", "Server Running",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Restore backup:\n{Path.GetFileName(backupPath)}\n\n" +
            "A safety backup of the current save data will be created first.\n\nContinue?",
            "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        AppendConsole($"[BACKUP] Restoring {Path.GetFileName(backupPath)}...", Color.FromArgb(156, 110, 201));
        bool ok = await _backupService.RestoreBackupAsync(backupPath);

        AppendConsole(ok
            ? "[BACKUP] Restore complete."
            : "[BACKUP] Restore failed. Check the log for details.",
            ok ? ThemeManager.StateRunning : ThemeManager.StateCrashed);

        RefreshBackupList();
    }

    private void DeleteSelectedBackup()
    {
        if (dgvBackups.SelectedRows.Count == 0)
        {
            MessageBox.Show("Select a backup to delete.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? backupPath = dgvBackups.SelectedRows[0].Tag as string;
        if (backupPath == null) return;

        var confirm = MessageBox.Show(
            $"Delete backup:\n{Path.GetFileName(backupPath)}\n\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        bool ok = _backupService.DeleteBackup(backupPath);
        AppendConsole(ok
            ? $"[BACKUP] Deleted: {Path.GetFileName(backupPath)}"
            : "[BACKUP] Failed to delete backup.",
            ok ? Color.Gray : ThemeManager.StateCrashed);

        RefreshBackupList();
    }

    internal void RefreshBackupList()
    {
        this.InvokeIfRequired(() =>
        {
            dgvBackups.Rows.Clear();
            var backups = _backupService.GetBackups();

            foreach (var (path, date, size) in backups)
            {
                string sizeMb = (size / 1024.0 / 1024.0).ToString("F1") + " MB";
                int rowIdx = dgvBackups.Rows.Add(Path.GetFileName(path), date.ToString("yyyy-MM-dd HH:mm:ss"), sizeMb);
                dgvBackups.Rows[rowIdx].Tag = path;
            }

            lblBackupCount.Text = $"{backups.Count} backup(s)";
        });
    }
}
