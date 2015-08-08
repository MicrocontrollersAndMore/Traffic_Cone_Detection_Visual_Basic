﻿'frmMain.vb
'
'Emgu CV 2.4.10
'
'add the following components to your form:
'tableLayoutPanel (TableLayoutPanel)
'btnOpenFile (Button)
'lblChosenFile (Label)
'ibOriginal (ImageBox)
'txtInfo (TextBox)
'cbShowSteps (CheckBox)
'ofdOpenFile (OpenFileDialog)

Option Explicit On      'require explicit declaration of variables, this is NOT Python !!
Option Strict On        'restrict implicit data type conversions to only widening conversions

Imports Emgu.CV                     '
Imports Emgu.CV.CvEnum              'Emgu Cv imports
Imports Emgu.CV.Structure           '
Imports Emgu.CV.UI                  '

'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Public Class frmMain

    ' module level variables ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Const IMAGE_BOX_PCT_SHOW_STEPS_NOT_CHECKED As Single = 75
    Const TEXT_BOX_PCT_SHOW_STEPS_NOT_CHECKED  As Single = 25

    Const IMAGE_BOX_PCT_SHOW_STEPS_CHECKED As Single = 55
    Const TEXT_BOX_PCT_SHOW_STEPS_CHECKED As Single = 45

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub frmMain_Load( sender As Object,  e As EventArgs) Handles MyBase.Load
        cbShowSteps_CheckedChanged(New Object, New EventArgs)
    End Sub
    
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub cbShowSteps_CheckedChanged( sender As Object,  e As EventArgs) Handles cbShowSteps.CheckedChanged
        If (cbShowSteps.Checked = False) Then
            tableLayoutPanel.RowStyles.Item(1).Height = IMAGE_BOX_PCT_SHOW_STEPS_NOT_CHECKED
            tableLayoutPanel.RowStyles.Item(2).Height = TEXT_BOX_PCT_SHOW_STEPS_NOT_CHECKED
        ElseIf (cbShowSteps.Checked = True) Then
            tableLayoutPanel.RowStyles.Item(1).Height = IMAGE_BOX_PCT_SHOW_STEPS_CHECKED
            tableLayoutPanel.RowStyles.Item(2).Height = TEXT_BOX_PCT_SHOW_STEPS_CHECKED
        End If
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub btnOpenFile_Click( sender As Object,  e As EventArgs) Handles btnOpenFile.Click
        Dim drChosenFile As DialogResult

        drChosenFile = ofdOpenFile.ShowDialog()                 'open file dialog
        
        If (drChosenFile <> Windows.Forms.DialogResult.OK Or ofdOpenFile.FileName = "") Then    'if user chose Cancel or filename is blank . . .
            lblChosenFile.Text = "file not chosen"              'show error message on label
            Return                                              'and exit function
        End If

        Dim imgOriginal As Image(Of Bgr, Byte)           'this is the main input image

        Try
            imgOriginal = New Image(Of Bgr, Byte)(ofdOpenFile.FileName)             'open image
        Catch ex As Exception                                                       'if error occurred
            lblChosenFile.Text = "unable to open image, error: " + ex.Message       'show error message on label
            Return                                                                  'and exit function
        End Try
        
        If imgOriginal Is Nothing Then                                  'if image could not be opened
            lblChosenFile.Text = "unable to open image"                 'show error message on label
            Return                                                      'and exit function
        End If

        lblChosenFile.Text = ofdOpenFile.FileName           'update label with file name

        Dim imgOriginalWithCones As Image(Of Bgr, Byte) = imgOriginal.Clone()
        Dim imgHSV As Image(Of Hsv, Byte)

        Dim imgThreshLow As Image(Of Gray, Byte)
        Dim imgThreshHigh As Image(Of Gray, Byte)

        Dim imgThresh As Image(Of Gray, Byte)
        
        Dim imgThreshSmoothed As Image(Of Gray, Byte)

        Dim imgCanny As Image(Of Gray, Byte)

        Dim imgContours As Image(Of Gray, Byte)

        Dim imgAllConvexHulls As Image(Of Bgr, Byte)
        Dim imgConvexHulls3To10 As Image(Of Bgr, Byte)
        Dim imgConvexHullsPointingUp As Image(Of Bgr, Byte)
        Dim imgTrafficConesWithOverlapsRemoved As Image(Of Bgr, Byte)

        Dim listOfContours As List(Of Contour(Of Point)) = New List(Of Contour(Of Point))
        Dim listOfTrafficCones As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))
        
        imgHSV = imgOriginal.Convert(Of Hsv, Byte)

        imgThreshLow = imgHSV.InRange(New Hsv(0, 135, 135), New Hsv(15, 255, 255))
        imgThreshHigh = imgHSV.InRange(New Hsv(159, 135, 135), New Hsv(179, 255, 255))
        
        imgThresh = imgThreshLow Or imgThreshHigh

        imgThreshSmoothed = imgThresh.Clone()

        imgThreshSmoothed._Erode(1)
        imgThreshSmoothed._Dilate(1)
        
        imgThreshSmoothed._SmoothGaussian(3)

        Dim dblCannyThreshold As Double = 160.0
        Dim dblThreshLinking As Double = 80.0

        imgCanny = imgThreshSmoothed.Canny(dblCannyThreshold, dblThreshLinking)

        Dim contours As Contour(Of Point) = imgCanny.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL)

        imgContours = imgThresh.CopyBlank()
        imgAllConvexHulls = imgOriginal.CopyBlank()
        imgConvexHulls3To10 = imgOriginal.CopyBlank()
        imgConvexHullsPointingUp = imgOriginal.CopyBlank()
        imgTrafficConesWithOverlapsRemoved = imgOriginal.CopyBlank()

        While (Not contours Is Nothing)
            Dim contour As Contour(Of Point) = contours.ApproxPoly(8.0)

            listOfContours.Add(contour)
            contours = contours.HNext
        End While

        For Each contour As Contour(Of Point) In listOfContours
            CvInvoke.cvDrawContours(imgContours, contour, New MCvScalar(255), New MCvScalar(255), 100, 1, LINE_TYPE.CV_AA, New Point(0, 0))

            Dim convexHull As Seq(Of Point) = contour.GetConvexHull(ORIENTATION.CV_CLOCKWISE)

            imgAllConvexHulls.Draw(convexHull, New Bgr(Color.Yellow), 2)

            If (convexHull.Total >= 3 And convexHull.Total <= 10) Then
                imgConvexHulls3To10.Draw(convexHull, New Bgr(Color.Yellow), 2)
            Else
                Continue For
            End If

            If (convexHullIsPointingUp(convexHull)) Then
                imgConvexHullsPointingUp.Draw(convexHull, New Bgr(Color.Yellow), 2)
            Else
                Continue For
            End If
                        'if convexHull passed all the conditionals, its a cone, so add to list
            listOfTrafficCones.Add(convexHull)
        Next

        listOfTrafficCones = removeInnerOverlappingCones(listOfTrafficCones)

        For Each trafficCone As Seq(Of Point) In listOfTrafficCones
            imgTrafficConesWithOverlapsRemoved.Draw(trafficCone, New Bgr(Color.Yellow), 2)
            imgOriginalWithCones.Draw(trafficCone, New Bgr(Color.Yellow), 2)
        Next

        txtInfo.AppendText("---------------------------------------" + vbCrLf + vbCrLf)

        If (listOfTrafficCones Is Nothing) Then
            txtInfo.AppendText("no traffic cones were found" + vbCrLf)
        ElseIf (listOfTrafficCones.Count = 0) Then
            txtInfo.AppendText("no traffic cones were found" + vbCrLf)
        ElseIf (listOfTrafficCones.Count = 1) Then
            txtInfo.AppendText("1 traffic cone was found" + vbCrLf)
        ElseIf (listOfTrafficCones.Count > 1) Then
            txtInfo.AppendText(listOfTrafficCones.Count.ToString() + " traffic cones were found" + vbCrLf)
        End If

        If (cbShowSteps.Checked = True) Then
            CvInvoke.cvShowImage("imgOriginal", imgOriginal)
            CvInvoke.cvShowImage("imgHSV", imgHSV)

            CvInvoke.cvShowImage("imgThreshLow", imgThreshLow)
            CvInvoke.cvShowImage("imgThreshHigh", imgThreshHigh)

            CvInvoke.cvShowImage("imgThresh", imgThresh)
            CvInvoke.cvShowImage("imgThreshSmoothed", imgThreshSmoothed)

            CvInvoke.cvShowImage("imgCanny", imgCanny)

            CvInvoke.cvShowImage("imgContours", imgContours)

            CvInvoke.cvShowImage("imgAllConvexHulls", imgAllConvexHulls)
            CvInvoke.cvShowImage("imgConvexHulls3To10", imgConvexHulls3To10)
            CvInvoke.cvShowImage("imgConvexHullsPointingUp", imgConvexHullsPointingUp)
            CvInvoke.cvShowImage("imgTrafficConesWithOverlapsRemoved", imgTrafficConesWithOverlapsRemoved)
        End If
        
        ibOriginal.Image = imgOriginalWithCones

    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function convexHullIsPointingUp(convexHull As Seq(Of Point)) As Boolean
        Dim dblAspectRatio As Double = CDbl(convexHull.BoundingRectangle.Width) / CDbl(convexHull.BoundingRectangle.Height)

        If (dblAspectRatio > 0.8) Then Return False

        Dim intYCenter As Integer = convexHull.BoundingRectangle.Y + CInt(CDbl(convexHull.BoundingRectangle.Height) / 2.0)

        Dim listOfPointsAboveCenter As List(Of Point) = New List(Of Point)
        Dim listOfPointsBelowCenter As List(Of Point) = New List(Of Point)

        For Each point As Point In convexHull
            If (point.Y < intYCenter) Then
                listOfPointsAboveCenter.Add(point)
            ElseIf (point.Y >= intYCenter) Then
                listOfPointsBelowCenter.Add(point)
            End If
        Next

        Dim intLeftMostPointBelowCenter As Integer = convexHull(0).X
        Dim intRightMostPointBelowCenter As Integer = convexHull(0).X

        For Each point As Point In listOfPointsBelowCenter
            If (point.X < intLeftMostPointBelowCenter) Then
                intLeftMostPointBelowCenter = point.X
            End If
        Next

        For Each point As Point In listOfPointsBelowCenter
            If (point.X > intRightMostPointBelowCenter) Then
                intRightMostPointBelowCenter = point.X
            End If
        Next

        For Each point As Point In listOfPointsAboveCenter
            If (point.X < intLeftMostPointBelowCenter Or point.X > intRightMostPointBelowCenter) Then
                Return False
            End If
        Next

        Return True
    End Function

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function removeInnerOverlappingCones(listOfCones As List(Of Seq(Of Point))) As List(Of Seq(Of Point))

        Dim listOfConesWithInnerCharRemoved As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))(listOfCones)

        For Each firstCone As Seq(Of Point) In listOfCones
            For Each secondCone As Seq(Of Point) In listOfCones
                If (Not firstCone.Equals(secondCone)) Then
                    
                    Dim firstConeMoments As MCvMoments = firstCone.GetMoments()
                    Dim secondConeMoments As MCvMoments = secondCone.GetMoments()

                    Dim sngFirstConeXCenterOfGravity As Single = CSng(firstConeMoments.GravityCenter.x)
                    Dim sngFirstConeYCenterOfGravity As Single = CSng(firstConeMoments.GravityCenter.y)
                    
                    Dim sngSecondConeXCenterOfGravity As Single = CSng(secondConeMoments.GravityCenter.x)
                    Dim sngSecondConeYCenterOfGravity As Single = CSng(secondConeMoments.GravityCenter.y)

                    Dim ptfFirstConeCenterOfGravity As PointF = New PointF(sngFirstConeXCenterOfGravity, sngFirstConeYCenterOfGravity)
                    Dim ptfSecondConeCenterOfGravity As PointF = New PointF(sngSecondConeXCenterOfGravity, sngSecondConeYCenterOfGravity)

                    If(firstCone.InContour(ptfSecondConeCenterOfGravity) > 0.0 Or secondCone.InContour(ptfFirstConeCenterOfGravity) > 0.0) Then
                                    'if we get in here we have found overlapping cones
                                    'next we identify which cone is smaller, then if that char was not already removed on a previous pass, remove it
                        If (firstCone.Area < secondCone.Area) Then                              'if first cone is smaller than second cone
                            If (listOfConesWithInnerCharRemoved.Contains(firstCone)) Then       'if first cone was not already removed on a previous pass . . .
                                listOfConesWithInnerCharRemoved.Remove(firstCone)               'then remove first cone
                            End If
                        ElseIf (secondCone.Area <= firstCone.Area) Then                         'else if second cone is smaller than first cone
                            If (listOfConesWithInnerCharRemoved.Contains(secondCone)) Then      'if second cone was not already removed on a previous pass . . .
                                listOfConesWithInnerCharRemoved.Remove(secondCone)              'then remove second cone
                            End If
                        End If
                    End If

                End If
            Next
        Next

        Return listOfConesWithInnerCharRemoved
    End Function

End Class
