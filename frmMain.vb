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

        Dim listOfTrafficCones As List(Of Seq(Of Point)) = findTrafficCones(imgOriginal)        'call find traffic cones function

        Dim imgOriginalWithCones As Image(Of Bgr, Byte) = imgOriginal.Clone()           'clone original image so we don't have to alter original image

        For Each trafficCone As Seq(Of Point) In listOfTrafficCones                     'draw found cones on image
            imgOriginalWithCones.Draw(trafficCone, New Bgr(Color.Yellow), 2)            'draw convex hull around outside of cone
            drawGreenDotAtConeCenter(trafficCone, imgOriginalWithCones)                 'draw small green dot at center of mass of cone
        Next

        txtInfo.AppendText("---------------------------------------" + vbCrLf + vbCrLf)
                                                                                            'show number of found traffic cones in info text box
        If (listOfTrafficCones Is Nothing) Then
            txtInfo.AppendText("no traffic cones were found" + vbCrLf + vbCrLf)
        ElseIf (listOfTrafficCones.Count = 0) Then
            txtInfo.AppendText("no traffic cones were found" + vbCrLf + vbCrLf)
        ElseIf (listOfTrafficCones.Count = 1) Then
            txtInfo.AppendText("1 traffic cone was found" + vbCrLf + vbCrLf)
        ElseIf (listOfTrafficCones.Count > 1) Then
            txtInfo.AppendText(listOfTrafficCones.Count.ToString() + " traffic cones were found" + vbCrLf + vbCrLf)
        End If
        
        ibOriginal.Image = imgOriginalWithCones                     'update image box on form
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function findTrafficCones(imgOriginal As Image(Of Bgr, Byte)) As List(Of Seq(Of Point))
        closeShowStepsWindows()                                 'close any windows that were open from a previous time this function was calles

        Dim imgHSV As Image(Of Hsv, Byte)                       'declare various images, the names should be self-explanatory

        Dim imgThreshLow As Image(Of Gray, Byte)
        Dim imgThreshHigh As Image(Of Gray, Byte)

        Dim imgThresh As Image(Of Gray, Byte)
        
        Dim imgThreshSmoothed As Image(Of Gray, Byte)

        Dim imgCanny As Image(Of Gray, Byte)

        Dim imgContours As Image(Of Gray, Byte)

        Dim imgAllConvexHulls As Image(Of Bgr, Byte)
        Dim imgConvexHulls3To10 As Image(Of Bgr, Byte)
        Dim imgTrafficCones As Image(Of Bgr, Byte)
        Dim imgTrafficConesWithOverlapsRemoved As Image(Of Bgr, Byte)

        Dim listOfContours As List(Of Contour(Of Point)) = New List(Of Contour(Of Point))                   'declare lists
        Dim listOfTrafficCones As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))
        Dim listOfTrafficConesWithOverlapsRemoved As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))    'this will be the return value
        
        imgHSV = imgOriginal.Convert(Of Hsv, Byte)                                  'convert to HSV color space, this will produce better color filtering

        imgThreshLow = imgHSV.InRange(New Hsv(0, 135, 135), New Hsv(15, 255, 255))          'threshold on low range of HSV red
        imgThreshHigh = imgHSV.InRange(New Hsv(159, 135, 135), New Hsv(179, 255, 255))      'threshold on high range of HSV red
        
        imgThresh = imgThreshLow Or imgThreshHigh                           'combine low range red thresh and high range red thresh

        imgThreshSmoothed = imgThresh.Clone()                       'clone thresh image before smoothing

        imgThreshSmoothed._Erode(1)                                 'open image
        imgThreshSmoothed._Dilate(1)                                '(erode, then dilate)
        
        imgThreshSmoothed._SmoothGaussian(3)                        'Gaussian blur

        Dim dblCannyThreshold As Double = 160.0                     'parameters for getting Canny edges
        Dim dblThreshLinking As Double = 80.0                       '

        imgCanny = imgThreshSmoothed.Canny(dblCannyThreshold, dblThreshLinking)         'get Canny edges

                                                                    'find external contours only
        Dim contours As Contour(Of Point) = imgCanny.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL)

        imgContours = imgThresh.CopyBlank()                         'instantiate remainnig images
        imgAllConvexHulls = imgOriginal.CopyBlank()
        imgConvexHulls3To10 = imgOriginal.CopyBlank()
        imgTrafficCones = imgOriginal.CopyBlank()
        imgTrafficConesWithOverlapsRemoved = imgOriginal.CopyBlank()

        While (Not contours Is Nothing)                                     'step through all external contours
            Dim contour As Contour(Of Point) = contours.ApproxPoly(8.0)     'approx poly to simplify shape to a degree

            listOfContours.Add(contour)                                     'add contour to list of contours
            contours = contours.HNext                                       'step to next contour
        End While

        For Each contour As Contour(Of Point) In listOfContours             'for each contour
                                                                            'draw on imgContours in case show steps is chosen
            CvInvoke.cvDrawContours(imgContours, contour, New MCvScalar(255), New MCvScalar(255), 100, 1, LINE_TYPE.CV_AA, New Point(0, 0))

            Dim convexHull As Seq(Of Point) = contour.GetConvexHull(ORIENTATION.CV_CLOCKWISE)       'get convex hull from contour

            imgAllConvexHulls.Draw(convexHull, New Bgr(Color.Yellow), 2)                'draw convex hull in case show steps is chosen
            
            If (convexHull.Total >= 3 And convexHull.Total <= 10) Then                  'if convex hull has at least 3 and less than 10 points,
                imgConvexHulls3To10.Draw(convexHull, New Bgr(Color.Yellow), 2)          'draw convex hull on applicable steps image and keep going
            Else
                Continue For            'else if convex hull had less than 3 points or more than 10 points, return to top of For without adding to list of cones
            End If

            If (convexHullIsPointingUp(convexHull)) Then        'if convex hull is pointing up . . .
                                                                    'if we get in here we have passed all the ifs, therefore the convex hull is a cone,
                listOfTrafficCones.Add(convexHull)                              'so add to list
                imgTrafficCones.Draw(convexHull, New Bgr(Color.Yellow), 2)      'and draw on traffic cones image
            Else
                Continue For            'else if convex hull was not pointing up, return to top of For without adding to list of cones
            End If
        Next
                                                                                                    'remove any inner overlapping cones,
        listOfTrafficConesWithOverlapsRemoved = removeInnerOverlappingCones(listOfTrafficCones)     'this will keep from counting the same cone multiple times
        
        For Each trafficCone As Seq(Of Point) In listOfTrafficConesWithOverlapsRemoved             'draw on final show steps image
            imgTrafficConesWithOverlapsRemoved.Draw(trafficCone, New Bgr(Color.Yellow), 2)         'draw cones
            drawGreenDotAtConeCenter(trafficCone, imgTrafficConesWithOverlapsRemoved)              'draw green dot at cone center
        Next

        If (cbShowSteps.Checked = True) Then                                            'show the show steps images if applicable
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
            CvInvoke.cvShowImage("imgTrafficCones", imgTrafficCones)
            CvInvoke.cvShowImage("imgTrafficConesWithOverlapsRemoved", imgTrafficConesWithOverlapsRemoved)
        End If

        Return listOfTrafficConesWithOverlapsRemoved
    End Function

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function convexHullIsPointingUp(convexHull As Seq(Of Point)) As Boolean
        Dim dblAspectRatio As Double = CDbl(convexHull.BoundingRectangle.Width) / CDbl(convexHull.BoundingRectangle.Height)     'calculate aspect ratio

        If (dblAspectRatio > 0.8) Then Return False                 'if convex hull is not taller than it is wide, return false

        Dim intYCenter As Integer = convexHull.BoundingRectangle.Y + CInt(CDbl(convexHull.BoundingRectangle.Height) / 2.0)      'calculate vertical center of convex hull

        Dim listOfPointsAboveCenter As List(Of Point) = New List(Of Point)          'declare list of points above vertical center
        Dim listOfPointsBelowCenter As List(Of Point) = New List(Of Point)          'and list of points below vertical center

        For Each point As Point In convexHull               'step through all points in convex hull
            If (point.Y < intYCenter) Then
                listOfPointsAboveCenter.Add(point)          'and add each point to list of points above or below vertical center as applicable
            ElseIf (point.Y >= intYCenter) Then
                listOfPointsBelowCenter.Add(point)
            End If
        Next

        Dim intLeftMostPointBelowCenter As Integer = convexHull(0).X            'declare and initialize left and right most points below center
        Dim intRightMostPointBelowCenter As Integer = convexHull(0).X

        For Each point As Point In listOfPointsBelowCenter                      'determine left most point below center
            If (point.X < intLeftMostPointBelowCenter) Then
                intLeftMostPointBelowCenter = point.X
            End If
        Next

        For Each point As Point In listOfPointsBelowCenter                      'determine right most point below center
            If (point.X > intRightMostPointBelowCenter) Then
                intRightMostPointBelowCenter = point.X
            End If
        Next

        For Each point As Point In listOfPointsAboveCenter          'step through all points above center
                                                                    'if any point is farther left or right than extreme left and right most lower points
            If (point.X < intLeftMostPointBelowCenter Or point.X > intRightMostPointBelowCenter) Then
                Return False                                        'then shape does not constitute pointing up, return false
            End If
        Next
                            'if we get here, shape has passed pointing up checks
        Return True         'return true
    End Function

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function removeInnerOverlappingCones(listOfCones As List(Of Seq(Of Point))) As List(Of Seq(Of Point))

        Dim listOfConesWithInnerCharRemoved As List(Of Seq(Of Point)) = New List(Of Seq(Of Point))(listOfCones)

        For Each firstCone As Seq(Of Point) In listOfCones              'step through list of cones with a nested for loop
            For Each secondCone As Seq(Of Point) In listOfCones         'to compare each cone to every other cone
                If (Not firstCone.Equals(secondCone)) Then                  'if we are not comparing a cone to itself
                    
                    Dim firstConeMoments As MCvMoments = firstCone.GetMoments()                         'calculate center of gravity of both cones
                    Dim secondConeMoments As MCvMoments = secondCone.GetMoments()

                    Dim sngFirstConeXCenterOfGravity As Single = CSng(firstConeMoments.GravityCenter.x)
                    Dim sngFirstConeYCenterOfGravity As Single = CSng(firstConeMoments.GravityCenter.y)
                    
                    Dim sngSecondConeXCenterOfGravity As Single = CSng(secondConeMoments.GravityCenter.x)
                    Dim sngSecondConeYCenterOfGravity As Single = CSng(secondConeMoments.GravityCenter.y)

                    Dim ptfFirstConeCenterOfGravity As PointF = New PointF(sngFirstConeXCenterOfGravity, sngFirstConeYCenterOfGravity)
                    Dim ptfSecondConeCenterOfGravity As PointF = New PointF(sngSecondConeXCenterOfGravity, sngSecondConeYCenterOfGravity)

                                                                        'if the center of gravity of either cone is inside the other cone . . .
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

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub drawGreenDotAtConeCenter(trafficCone As Seq(Of Point), ByRef image As Image(Of Bgr, Byte))
        Dim coneMoments As MCvMoments = trafficCone.GetMoments()                            'get moments

        Dim sngConeXCenterOfGravity As Single = CSng(coneMoments.GravityCenter.x)           'get x center of gravity
        Dim sngConeYCenterOfGravity As Single = CSng(coneMoments.GravityCenter.y)           'get y center of gravity

        Dim ptfFirstConeCenterOfGravity As PointF = New PointF(sngConeXCenterOfGravity, sngConeYCenterOfGravity)        'assign point (x, y) center of gravity

        Dim cfCenterOfCone As CircleF = New CircleF(ptfFirstConeCenterOfGravity, 3)         'declare circle at center of gravity with radius of 3

        image.Draw(cfCenterOfCone, New Bgr(0, 255, 0), 0)                                   'draw circle in image (image is pass by reference)
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub closeShowStepsWindows()
        CvInvoke.cvDestroyWindow("imgOriginal")
        CvInvoke.cvDestroyWindow("imgHSV")
        CvInvoke.cvDestroyWindow("imgThreshLow")
        CvInvoke.cvDestroyWindow("imgThreshHigh")
        CvInvoke.cvDestroyWindow("imgThresh")
        CvInvoke.cvDestroyWindow("imgThreshSmoothed")
        CvInvoke.cvDestroyWindow("imgCanny")
        CvInvoke.cvDestroyWindow("imgContours")
        CvInvoke.cvDestroyWindow("imgAllConvexHulls")
        CvInvoke.cvDestroyWindow("imgConvexHulls3To10")
        CvInvoke.cvDestroyWindow("imgTrafficCones")
        CvInvoke.cvDestroyWindow("imgTrafficConesWithOverlapsRemoved")
    End Sub
    
End Class
