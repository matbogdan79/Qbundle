﻿Public Class frmMiner
    Private Sub frmMiner_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        lstPlots.Items.Clear()
        If Q.settings.Plots <> "" Then
            Dim buffer() As String = Split(Q.settings.Plots, "|")
            For Each plot As String In buffer
                If plot.Length > 1 Then
                    lstPlots.Items.Add(plot)
                End If
            Next
        End If

        cmlServers.Items.Clear()
        Dim mnuitm As ToolStripMenuItem
        For t As Integer = 0 To UBound(QGlobal.Pools)
            mnuitm = New ToolStripMenuItem
            mnuitm.Name = QGlobal.Pools(t).Name
            mnuitm.Text = QGlobal.Pools(t).Name
            AddHandler(mnuitm.Click), AddressOf SelectPoolID
            cmlServers.Items.Add(mnuitm)

        Next

    End Sub
    Private Sub SelectPoolID(sender As Object, e As EventArgs)

        For x As Integer = 0 To UBound(QGlobal.Pools)
            If sender.text = QGlobal.Pools(x).Name Then
                txtMiningServer.Text = QGlobal.Pools(x).Address
                nrMiningPort.Value = Val(QGlobal.Pools(x).Port)
                txtUpdateServer.Text = QGlobal.Pools(x).Address
                nrUpdatePort.Value = Val(QGlobal.Pools(x).Port)
                txtInfoServer.Text = QGlobal.Pools(x).Address
                nrInfoPort.Value = Val(QGlobal.Pools(x).Port)
                txtDeadLine.Text = QGlobal.Pools(x).DeadLine
                Exit For
            End If
        Next

    End Sub

    Private Sub btnImport_Click(sender As Object, e As EventArgs) Handles btnImport.Click

        Dim ofd As New OpenFileDialog
        If ofd.ShowDialog = DialogResult.OK Then
            If IO.File.Exists(ofd.FileName) Then
                lstPlots.Items.Add(ofd.FileName)
                Q.settings.Plots &= ofd.FileName & "|"
                Q.settings.SaveSettings()
            End If
        End If
    End Sub

    Private Sub btnRemove_Click(sender As Object, e As EventArgs) Handles btnRemove.Click
        If lstPlots.SelectedIndex = -1 Then
            MsgBox("You need to select a plot to remove.", MsgBoxStyle.Information Or MsgBoxStyle.OkOnly, "Nothing to remove")
            Exit Sub
        End If
        If MsgBox("Are you sure you want to remove selected plot?" & vbCrLf & "It will not be deleted from disk.", MsgBoxStyle.Exclamation Or MsgBoxStyle.YesNo, "Remove plotfile") = MsgBoxResult.Yes Then
            lstPlots.Items.RemoveAt(lstPlots.SelectedIndex)
            Q.settings.Plots = ""
            For t As Integer = 0 To lstPlots.Items.Count - 1
                Q.settings.Plots &= lstPlots.Items.Item(t) & "|"
            Next
            Q.settings.SaveSettings()

        End If
    End Sub

    Private Sub btnPool_Click(sender As Object, e As EventArgs) Handles btnPool.Click
        Try
            Me.cmlServers.Show(Me.btnPool, Me.btnPool.PointToClient(Cursor.Position))
        Catch ex As Exception

        End Try
    End Sub

    Private Sub btnStartMine_Click(sender As Object, e As EventArgs) Handles btnStartMine.Click

        'generic checks

        Dim PassPhrase As String = ""

        If lstPlots.Items.Count = 0 Then
            MsgBox("You need to add your plots to My plotfiles in the bottom before you can mine.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "No plotfiles")
            Exit Sub
        End If
        If Not IsNumeric(txtDeadLine.Text) Then
            MsgBox("Your deadline is not a numeric value.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "No deadline")
            Exit Sub
        End If

        If rbSolo.Checked Then
            'do checks for solo
            If Not frmMain.Running Then
                MsgBox("Your local wallet is not running. Please start your local wallet and make sure it is synced.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "No wallet to mine against")
                Exit Sub
            End If

            Dim AccountId As String = GetAccountIdFromPlot(lstPlots.Items.Item(0))
            If lstPlots.Items.Count > 1 Then
                For t As Integer = 1 To lstPlots.Items.Count - 1
                    If Not AccountId = GetAccountIdFromPlot(lstPlots.Items(t)) Then
                        MsgBox("When mining solo you cannot mine with different accounts at the same time. Please remove the plotfiles with account id that does not match the account you want to mine with.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Multiple accounts.")
                        Exit Sub
                    End If
                Next
            End If

            'check account and ask for pin or passphrase
            'check first for account and ask for pin

            For Each account As QB.clsAccounts.Account In Q.Accounts.AccArray
                If account.AccountID = AccountId Then
                    Dim pin As String = InputBox("Enter pin for account " & account.AccountName & " (" & account.RSAddress & ") used in plotfiles.", "Enter Pin", "")
                    If pin.Length > 0 Then
                        Dim tmp As String = Q.Accounts.GetPassword(account.AccountName, pin)
                        If tmp.Length > 0 Then
                            PassPhrase = tmp
                        Else
                            MsgBox("You entered the wrong pin.", MsgBoxStyle.Exclamation Or MsgBoxStyle.OkOnly, "Wrong Pin")
                            Exit Sub
                        End If
                    Else
                        MsgBox("You entered the wrong pin.", MsgBoxStyle.Exclamation Or MsgBoxStyle.OkOnly, "Wrong Pin")
                        Exit Sub
                    End If
                    Exit For
                End If
            Next
            If PassPhrase.Length = 0 Then
                Dim tmp As String = InputBox("Enter passphrase for account BURST-" & Q.Accounts.ConvertIdToRS(AccountId) & " (" & AccountId & ") used in plotfiles.", "Enter Passphrase", "")
                If tmp.Length > 0 Then
                    If AccountId = Q.Accounts.GetAccountIDFromPassPhrase(tmp) Then
                        PassPhrase = tmp
                    Else
                        MsgBox("Passphrase does not match accountid in plotfiles.", MsgBoxStyle.Exclamation Or MsgBoxStyle.OkOnly, "Wrong passphrase")
                        Exit Sub
                    End If
                Else
                    MsgBox("You entered the wrong passphrase.", MsgBoxStyle.Exclamation Or MsgBoxStyle.OkOnly, "Wrong passphrase")
                    Exit Sub
                End If
            End If



        Else
            'we need to do additional checks when it comes to pool settings.
            If txtMiningServer.Text.Length < 4 Or Not txtMiningServer.Text.Contains(".") Then
                MsgBox("You need to enter a valid mining server address.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Mining server missing")
                Exit Sub
            End If
            If txtUpdateServer.Text.Length < 4 Or Not txtUpdateServer.Text.Contains(".") Then
                MsgBox("You need to enter a valid update server address.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Update server missing")
                Exit Sub
            End If
            If txtInfoServer.Text.Length < 4 Or Not txtInfoServer.Text.Contains(".") Then
                MsgBox("You need to enter a valid info server address.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Info server missing")
                Exit Sub
            End If

            If txtMiningServer.Text.Contains("/") Then
                MsgBox("Mining server shold not be defined as a url.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Mining server wrong")
                Exit Sub
            End If

            If txtUpdateServer.Text.Contains("/") Then
                MsgBox("Update server shold not be defined as a url.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Update server wrong")
                Exit Sub
            End If

            If txtInfoServer.Text.Contains("/") Then
                MsgBox("Info server shold not be defined as a url.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Info server wrong")
                Exit Sub
            End If

            'now we can only asume it is correct.

        End If
        Dim msg As String = ""
        msg &= "Blago Miner is not installed yet. Do you want to download and install it now?" & vbCrLf & vbCrLf
        msg &= "Please be advised that Miners can be detected as malware by antimalware software." & vbCrLf
        msg &= "If you have a antimalware software you might need to whitelist the miner." & vbCrLf

        Q.App.SetLocalInfo() 'do a precheck due to antivir removals
        If Not Q.App.isInstalled(QGlobal.AppNames.BlagoMiner) Then
            If MsgBox(msg, MsgBoxStyle.Exclamation Or MsgBoxStyle.YesNo, "Download Miner") = MsgBoxResult.Yes Then
                Dim s As frmDownloadExtract = New frmDownloadExtract
                s.Appid = QGlobal.AppNames.Xplotter
                Dim res As DialogResult
                res = s.ShowDialog
                If res = DialogResult.Cancel Then
                    Exit Sub
                ElseIf res = DialogResult.Abort Then
                    MsgBox("Something went wrong. Internet connection might have been lost.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Error")
                    Exit Sub
                End If
            Else
                Exit Sub
            End If
        End If
        Threading.Thread.Sleep(100)
        Q.App.SetLocalInfo() 'update to check that it really is installed.
        If Not Q.App.isInstalled(QGlobal.AppNames.BlagoMiner) Then
            MsgBox("Miner was downloaded but removed. Please check Antimalware software.", MsgBoxStyle.Critical Or MsgBoxStyle.OkOnly, "Error")
            Exit Sub
        End If

        'ok all seems fine. if solo then write password file
        If PassPhrase.Length > 0 Then
            System.IO.File.WriteAllText(QGlobal.BaseDir & "\BlagoMiner\passphrases.txt", PassPhrase)
        End If

        Try
            Dim p As Process = New Process
            p.StartInfo.WorkingDirectory = QGlobal.BaseDir & "BlagoMiner"
            p.StartInfo.Arguments = ""
            p.StartInfo.UseShellExecute = True
            If QGlobal.CPUInstructions.AVX2 Then
                p.StartInfo.FileName = QGlobal.BaseDir & "BlagoMiner\BlagoMiner_avx2.exe"
            ElseIf QGlobal.CPUInstructions.AVX Then
                p.StartInfo.FileName = QGlobal.BaseDir & "BlagoMiner\BlagoMiner_avx.exe"
            ElseIf QGlobal.CPUInstructions.SSE Then
                p.StartInfo.FileName = QGlobal.BaseDir & "BlagoMiner\BlagoMiner_sse.exe"
            End If
            p.StartInfo.Verb = "runas"
            p.Start()
        Catch ex As Exception
            MsgBox("Failed to start Blago miner")
        End Try


    End Sub
    Private Function GetAccountIdFromPlot(ByVal Plotfile As String) As String
        Dim filename As String = IO.Path.GetFileName(Plotfile)
        Dim buffer() As String = Split(filename, "_")
        Return buffer(0)
    End Function
    Private Sub WriteConfig()
        Dim plots As String = ""
        For t As Integer = 0 To lstPlots.Items.Count - 1
            plots &= Chr(34) & IO.Path.GetDirectoryName(lstPlots.Items.Item(t)) & Chr(34) & ","
        Next
        plots = Replace(plots, "\", "\\")
        plots = plots.Substring(0, plots.Length - 1)
        Dim Mode As String = ""
        If rbPool.Checked Then
            Mode = "pool"
        Else
            Mode = "solo"
        End If

        Dim UseHdd As String = "false"
        Dim ShowWInner As String = "false"
        Dim UseBoost As String = "false"

        If chkUseHDD.Checked Then UseHdd = "true"
        If chkUseBoost.Checked Then UseBoost = "true"
        If chkShowWinner.Checked Then ShowWInner = "true"

        Dim cfg As String = ""
        cfg &= "{" & vbCrLf
        cfg &= "   " & Chr(34) & "Mode" & Chr(34) & " : " & Chr(34) & Mode & Chr(34) & "," & vbCrLf
        cfg &= "   " & Chr(34) & "Server" & Chr(34) & " : " & Chr(34) & txtMiningServer.Text & Chr(34) & "," & vbCrLf
        cfg &= "   " & Chr(34) & "Port" & Chr(34) & ": " & nrMiningPort.Value.ToString & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "UpdaterAddr" & Chr(34) & " : " & Chr(34) & txtUpdateServer.Text & Chr(34) & "," & vbCrLf
        cfg &= "   " & Chr(34) & "UpdaterPort" & Chr(34) & ": " & Chr(34) & nrMiningPort.Value.ToString & Chr(34) & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "InfoAddr" & Chr(34) & " : " & Chr(34) & txtInfoServer.Text & Chr(34) & "," & vbCrLf
        cfg &= "   " & Chr(34) & "InfoPort" & Chr(34) & ": " & Chr(34) & nrInfoPort.Value.ToString & Chr(34) & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "EnableProxy" & Chr(34) & ": false," & vbCrLf
        cfg &= "   " & Chr(34) & "ProxyPort" & Chr(34) & ": 8126," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "Paths" & Chr(34) & ":[" & plots & "]," & vbCrLf
        cfg &= "   " & Chr(34) & "CacheSize" & Chr(34) & " : 10000," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "Debug" & Chr(34) & ": true," & vbCrLf
        cfg &= "   " & Chr(34) & "UseHDDWakeUp" & Chr(34) & ": " & UseHdd & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "TargetDeadline" & Chr(34) & ": " & txtDeadLine.Text & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "SendInterval" & Chr(34) & ": 100," & vbCrLf
        cfg &= "   " & Chr(34) & "UpdateInterval" & Chr(34) & ": 950," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "UseLog" & Chr(34) & " : true," & vbCrLf
        cfg &= "   " & Chr(34) & "ShowWinner" & Chr(34) & " : " & ShowWInner & "," & vbCrLf
        cfg &= "   " & Chr(34) & "UseBoost" & Chr(34) & " : " & UseBoost & "," & vbCrLf
        cfg &= "" & vbCrLf
        cfg &= "   " & Chr(34) & "WinSizeX" & Chr(34) & ": 76," & vbCrLf
        cfg &= "   " & Chr(34) & "WinSizeY" & Chr(34) & ": 60" & vbCrLf
        cfg &= "}" & vbCrLf
    End Sub

    Private Sub rbPool_CheckedChanged(sender As Object, e As EventArgs) Handles rbPool.Click

        pnlPool.Enabled = True

    End Sub

    Private Sub rbSolo_CheckedChanged(sender As Object, e As EventArgs) Handles rbSolo.Click
        pnlPool.Enabled = False

        Generic.UpdateLocalWallet()

        Dim buffer() As String = Split(Replace(QGlobal.Wallets(0).Address, "http://", ""), ":")
        nrMiningPort.Value = Val(buffer(1))
        nrUpdatePort.Value = Val(buffer(1))
        nrInfoPort.Value = Val(buffer(1))
        txtMiningServer.Text = buffer(0)
        txtUpdateServer.Text = buffer(0)
        txtInfoServer.Text = buffer(0)
        txtDeadLine.Text = "86400"


    End Sub
End Class