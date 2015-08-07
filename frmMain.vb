'frmMain.vb
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

        Dim imgSmoothed As Image(Of Bgr, Byte)
        Dim imgGrayColorFiltered As Image(Of Gray, Byte)
        Dim imgCanny As Image(Of Gray, Byte)
        Dim imgContours As Image(Of Bgr, Byte)
        Dim imgAllConvexHulls As Image(Of Bgr, Byte)
        Dim imgConvexHulls3To7 As Image(Of Bgr, Byte)
        Dim imgConvexHullsPointingUp As Image(Of Bgr, Byte)
        Dim imgPolygons As Image(Of Bgr, Byte)

        'imgOriginal._EqualizeHist()

        imgSmoothed = imgOriginal.PyrDown().PyrUp()
        imgSmoothed._SmoothGaussian(3)

        imgGrayColorFiltered = imgSmoothed.InRange(New Bgr(0, 0, 150), New Bgr(85, 125, 255))
		imgGrayColorFiltered = imgGrayColorFiltered.PyrDown().PyrUp()						'repeat smoothing process after InRange function call,
		imgGrayColorFiltered._SmoothGaussian(3)	

        Dim grayCannyThreshold As Gray = New Gray(160)
        Dim grayThreshLinking As Gray = New Gray(80)

        imgGrayColorFiltered._Erode(1)
        imgGrayColorFiltered._Dilate(1)

        imgCanny = imgGrayColorFiltered.Canny(160, 80)

        Dim contours As Contour(Of Point) = imgCanny.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_LIST)
        
        Dim listOfContours As List(Of Contour(Of Point)) = New List(Of Contour(Of Point))
        Dim listOfConvexHulls As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))

        imgContours = imgOriginal.CopyBlank()
        imgAllConvexHulls = imgOriginal.CopyBlank()
        imgConvexHulls3To7 = imgOriginal.CopyBlank()
        imgConvexHullsPointingUp = imgOriginal.CopyBlank()
        imgPolygons = imgOriginal.CopyBlank()

        While (Not contours Is Nothing)
            'Dim contour As Contour(Of Point) = contours.ApproxPoly(10.0)
            Dim contour As Contour(Of Point) = contours.ApproxPoly(10.0)

            CvInvoke.cvDrawContours(imgContours, contour, New MCvScalar(255, 255, 255), New MCvScalar(255, 255, 255), 100, 1, LINE_TYPE.CV_AA, New Point(0, 0))

            Dim convexHull As Seq(Of Point) = contour.GetConvexHull(ORIENTATION.CV_CLOCKWISE)

            imgAllConvexHulls.Draw(convexHull, New Bgr(Color.Yellow), 2)

            If (convexHull.Total >= 3 And convexHull.Total <= 8) Then
                imgConvexHulls3To7.Draw(convexHull, New Bgr(Color.Yellow), 2)
                If (convexHullIsPointingUp(convexHull)) Then
                    imgConvexHullsPointingUp.Draw(convexHull, New Bgr(Color.Yellow), 2)
                    listOfConvexHulls.Add(convexHull)
                End If
            End If

            'CvInvoke.cvWaitKey(0)
            'Application.DoEvents()

            contours = contours.HNext
        End While

        For Each convexHull As Seq(Of Point) In listOfConvexHulls
            imgPolygons.Draw(convexHull, New Bgr(Color.Yellow), 2)
            imgOriginal.Draw(convexHull, New Bgr(Color.Yellow), 2)
        Next

        CvInvoke.cvShowImage("imgSmoothed", imgSmoothed)
        CvInvoke.cvShowImage("imgGrayColorFiltered", imgGrayColorFiltered)
        CvInvoke.cvShowImage("imgCanny", imgCanny)
        CvInvoke.cvShowImage("imgContours", imgContours)
        CvInvoke.cvShowImage("imgAllConvexHulls", imgAllConvexHulls)
        CvInvoke.cvShowImage("imgConvexHulls3To7", imgConvexHulls3To7)
        CvInvoke.cvShowImage("imgConvexHullsPointingUp", imgConvexHullsPointingUp)
        CvInvoke.cvShowImage("imgPolys", imgPolygons)

        ibOriginal.Image = imgOriginal

    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function convexHullIsPointingUp(convexHull As Seq(Of Point)) As Boolean

        Dim dblAspectRatio As Double = CDbl(convexHull.BoundingRectangle.Width) / CDbl(convexHull.BoundingRectangle.Height)

        If (dblAspectRatio > 0.75) Then Return False

        Dim moments As MCvMoments = convexHull.GetMoments()

        Dim intXCenterOfMass As Integer = CInt(moments.GravityCenter.x)
        Dim intYCenterOfMass As Integer = CInt(moments.GravityCenter.y)
        
        Dim intNumPointsAboveCenterOfMass As Integer = 0

        For Each point As Point In convexHull
            If (point.Y < intYCenterOfMass) Then
                intNumPointsAboveCenterOfMass = intNumPointsAboveCenterOfMass + 1
            End If
        Next

        Dim dblFractionOfPointsAboveCenterOfMass As Double = CDbl(intNumPointsAboveCenterOfMass) / CDbl(convexHull.Total)

        'If (dblFractionOfPointsAboveCenterOfMass >= 0.5) Then Return False
        
        Dim listOfPointsAboveCenterOfMass As List(Of Point) = New List(Of Point)
        Dim listOfPointsBelowCenterOfMass As List(Of Point) = New List(Of Point)

        For Each point As Point In convexHull
            If (point.Y < intYCenterOfMass) Then
                listOfPointsAboveCenterOfMass.Add(point)
            ElseIf (point.Y > intYCenterOfMass) Then
                listOfPointsBelowCenterOfMass.Add(point)
            End If
        Next

        Dim intLeftMostPointBelowCenterOfMass As Integer = 1000000
        Dim intRightMostPointBelowCenterOfMass As Integer = -1000000

        For Each point As Point In listOfPointsBelowCenterOfMass
            If (point.X < intLeftMostPointBelowCenterOfMass) Then
                intLeftMostPointBelowCenterOfMass = point.X
            End If
        Next

        For Each point As Point In listOfPointsBelowCenterOfMass
            If (point.X > intRightMostPointBelowCenterOfMass) Then
                intRightMostPointBelowCenterOfMass = point.X
            End If
        Next

        For Each point As Point In listOfPointsAboveCenterOfMass
            If (point.X < intLeftMostPointBelowCenterOfMass Or point.X > intRightMostPointBelowCenterOfMass) Then
                Return False
            End If
        Next

        Return True
    End Function

End Class







