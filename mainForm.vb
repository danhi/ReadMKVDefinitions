Imports System.IO
Imports System.Text
Imports System.Xml
Public Class mainForm
#Region "Properties"
    Private defFilename As String = My.Computer.FileSystem.CurrentDirectory & "\MKVDefinitions.txt"
    Private codeFileName As String = "c:\source\mkvinfo\mkvinfoCodeDef.vb"
    Private Structure EBMLItem
        Public category, name, ID, description As String
        Public level As Integer
        Public type As Char
        Public mandatory, container, hasDefault As Boolean
        Public Overrides Function ToString() As String
            Return name
        End Function
    End Structure
    Private Structure DataType
        Public chType As Char
        Public description As String
        Public Sub New(c As Char, d As String)
            chType = c
            description = d
        End Sub
    End Structure
    Private dataTypes() As DataType = {New DataType("m"c, "Master"),
                                                               New DataType("u"c, "unsigned int"),
                                                               New DataType("i"c, "signed int"),
                                                               New DataType("s"c, "string"),
                                                               New DataType("8"c, "UTF-8 string"),
                                                               New DataType("b"c, "binary"),
                                                               New DataType("f"c, "float"),
                                                               New DataType("d"c, "date")}
    Private EBMLItems As New Dictionary(Of String, EBMLItem)
    Private mandatoryItems As New Hashtable
#End Region
    Private Sub mainform_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        Dim el As New EBMLItem

        ReadDefinitions()
        SetContainerFlag()
        BuildMandatoryItems()
        el.name = ""
        DecodeEBML(el)
    End Sub
    Private Sub btnSearchByName_Click(sender As System.Object, e As System.EventArgs) Handles btnSearchByName.Click
        Dim st As New SearchTerm

        st.Text = "Search By Element Name"
        If st.ShowDialog = Windows.Forms.DialogResult.OK Then
            'el = GetEBMLElementByName(st.tbSearch.Text)
            GetTNByName(tvItems.Nodes, st.tbSearch.Text)
            Return
        Else
            MsgBox("No container with name " & st.tbSearch.Text)
        End If
    End Sub
    Private Sub btnSearchByID_Click(sender As System.Object, e As System.EventArgs) Handles btnSearchByID.Click
        Dim st As New SearchTerm

        st.Text = "Search By Element ID"
        If st.ShowDialog = Windows.Forms.DialogResult.OK Then
            If Not GetTNByID(tvItems.Nodes, st.tbSearch.Text.ToUpper) Then
                MsgBox("No container with ID " & st.tbSearch.Text.ToUpper)
            End If
        End If
    End Sub
    Private Sub btnGenerateCode_Click(sender As System.Object, e As System.EventArgs) Handles btnGenerateCode.Click
        Dim sw As StreamWriter
        Dim tab As Char = ControlChars.Tab
        Dim q As Char = ControlChars.Quote
        Dim sep As String = q & ", " & q
        Dim desc As String
        Dim sb As StringBuilder
        Dim miSep As String = Nothing
        Dim trulyMandatory As Boolean

        Dim od As New OpenFileDialog

        od.AddExtension = True
        od.Multiselect = False
        od.CheckFileExists = False
        od.Filter = "VB Code file (*.vb)|*.vb"
        od.Title = "Create VB stub file"
        If od.ShowDialog() = Windows.Forms.DialogResult.OK Then
            If File.Exists(od.FileName) Then
                If MsgBox(od.FileName & "exists, overwrite?", MsgBoxStyle.YesNo) = MsgBoxResult.No Then
                    Return
                End If
            End If
            sw = New StreamWriter(od.FileName)
        Else
            Return
        End If
        sw.WriteLine("Module [Global]")
        sw.WriteLine(tab & "Friend Structure MKVItem")
        sw.WriteLine(tab & tab & "Public category, name, ID, description As String")
        sw.WriteLine(tab & tab & "Public level As Integer")
        sw.WriteLine(tab & tab & " Public type As Char")
        sw.WriteLine(tab & tab & "Public mandatory, container As Boolean")
        sw.WriteLine()
        sw.WriteLine(tab & tab & tab & "Public Sub New(c As String, n As String, i As String, d As String, l As Integer, t As Char, m As Boolean, cnt as boolean)")
        sw.WriteLine(tab & tab & tab & "category = c")
        sw.WriteLine(tab & tab & tab & "Name = n")
        sw.WriteLine(tab & tab & tab & "ID = i")
        sw.WriteLine(tab & tab & tab & "description = d")
        sw.WriteLine(tab & tab & tab & "level = l")
        sw.WriteLine(tab & tab & tab & "Type = t")
        sw.WriteLine(tab & tab & tab & "mandatory = m")
        sw.WriteLine(tab & tab & tab & "container = cnt")
        sw.WriteLine(tab & tab & "End Sub")
        sw.WriteLine(tab & tab & "Public Overrides Function ToString() As String")
        sw.WriteLine(tab & tab & "Return name")
        sw.WriteLine(tab & tab & "End Function")
        sw.WriteLine(tab & "End Structure")
        sw.WriteLine(tab & "Friend Structure MandatoryItem")
        sw.WriteLine(tab & tab & "Public container As String")
        sw.WriteLine(tab & tab & "Public mandatoryItems() As String")
        sw.WriteLine()
        sw.WriteLine(tab & tab & "Public Sub New(c As String, al() As String)")
        sw.WriteLine(tab & tab & tab & "container = c")
        sw.WriteLine(tab & tab & tab & "mandatoryItems = al")
        sw.WriteLine(tab & tab & "End Sub")
        sw.WriteLine(tab & "End Structure")
        sw.WriteLine("Friend MKVItems As New Hashtable")
        sw.WriteLine()
        sw.WriteLine(tab & "Friend mandatoryItems() As mandatoryItem = {")
        For Each key As String In mandatoryItems.Keys
            sb = New StringBuilder
            If miSep = "" Then
                miSep = ", " & vbCrLf
            Else
                sb.Append(miSep)
            End If
            sb.Append(tab & tab & "New mandatoryItem(" & q & key & q & ", {")
            For Each v As String In mandatoryItems(key)
                sb.Append(q & v & q & ", ")
            Next
            sb.Remove(sb.Length - 2, 2)
            sb.Append("})")
            sw.Write(sb.ToString)
        Next
        sw.WriteLine("}")
        sw.WriteLine(tab & "Friend Sub BuildMKVItems()")

        For Each el As EBMLItem In EBMLItems.Values
            If el.mandatory AndAlso Not el.hasDefault Then
                trulyMandatory = True
            Else
                trulyMandatory = False
            End If
            desc = el.description.Replace(q, q & " & " & q).Replace(ControlChars.CrLf, " ")
            sw.WriteLine(tab & tab & "MKVItems.Add(" & q & el.ID & q & ", " & "new MKVItem(" & q & el.category & sep &
                         el.name & sep & el.ID.ToString & sep & desc & q & ", " & el.level.ToString &
                         ", " & q & el.type.ToString & q & "c, " & q & trulyMandatory.ToString & q & ", " & q & el.container.ToString & q & "))")
        Next
        sw.WriteLine(tab & "End Sub")
        sw.WriteLine("End Module")
        sw.Close()
    End Sub
    Private Sub ReadDefinitions()
        Dim s, category As String
        category = "" ' just for compiler warning
        Dim el, elNext As EBMLItem
        Dim tn As New TreeNode
        Dim xr As New XmlTextReader(defFilename)

        ' first build the items hashtable
        el = Nothing ' just to get rid of compiler warning
        While xr.Read
            If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "tr" AndAlso xr.AttributeCount > 0 Then
                s = xr.GetAttribute("class")
                If s = "toptitle" Then
                    category = GetCategory(xr)
                ElseIf s.StartsWith("level") OrElse s.StartsWith("version") Then
                    If Not ReadRow(xr, el, s) Then
                        MsgBox("Failed reading at line " & xr.LineNumber)
                        Return
                    Else
                        el.category = category
                        EBMLItems.Add(el.ID, el)
                    End If
                End If
            End If
        End While
        ' now build the treelist
        For i As Integer = 0 To EBMLItems.Values.Count - 1
            el = EBMLItems.Values(i)
            tn = New TreeNode(el.name)
            If Not i = EBMLItems.Values.Count - 1 Then
                elNext = EBMLItems.Values(i + 1)
                If el.category = elNext.category AndAlso elNext.level > el.level Then ' toplevel of a category is like a level 0
                    If Not ProcessEntry(tn, i) Then
                        tvItems.Nodes.Add(tn)
                        Return
                    End If
                End If
            End If
            tvItems.Nodes.Add(tn)
        Next
    End Sub
    Private Sub SetContainerFlag()
        Dim el, elNext As EBMLItem

        For i As Integer = 0 To EBMLItems.Count - 2
            el = EBMLItems.Values(i)
            elNext = EBMLItems.Values(i + 1)
            If elNext.level > el.level Then
                el.container = True
                EBMLItems(el.ID) = el
            End If
        Next
    End Sub
    Private Sub BuildMandatoryItems()
        For Each tn As TreeNode In tvItems.Nodes
            BuildMandatoryItemsFromNode(tn)
        Next
    End Sub
    Private Sub BuildMandatoryItemsFromNode(parentTN As TreeNode)
        Dim al As New ArrayList
        Dim el As EBMLItem

        For Each tn As TreeNode In parentTN.Nodes
            el = GetEBMLElementByName(tn.Text)
            If el.mandatory AndAlso Not el.hasDefault Then
                al.Add(el.name)
            End If
            If tn.Nodes.Count > 0 Then
                BuildMandatoryItemsFromNode(tn)
            End If
        Next
        If al.Count > 0 Then
            mandatoryItems.Add(parentTN.Text, al)
        End If
    End Sub
    Private Function GetCategory(xr As XmlTextReader) As String

        While xr.Read
            If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "tr" Then
                While xr.Read
                    If xr.NodeType = XmlNodeType.Text Then
                        Return xr.Value
                    ElseIf xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "tr" Then
                        Exit While
                    End If
                End While
                Exit While
            End If
        End While
        Return ""
    End Function
    Private Function ProcessEntry(ByRef parentTN As TreeNode, ByRef elSub As Integer) As Boolean
        Dim el, elNext As EBMLItem
        Dim tn As TreeNode = New TreeNode

        elSub += 1
        While elSub < EBMLItems.Values.Count
            el = EBMLItems.Values(elSub)
            tn = New TreeNode(el.name)
            If Not elSub = EBMLItems.Values.Count - 1 Then
                elNext = EBMLItems.Values(elSub + 1)
                If Not el.category = elNext.category Then
                    ' toplevel of a category is like a level 0
                    parentTN.Nodes.Add(tn)
                    Return True
                ElseIf elNext.level < el.level Then
                    parentTN.Nodes.Add(tn)
                    Return True
                ElseIf elNext.level = el.level Then
                    parentTN.Nodes.Add(tn)
                Else
                    If Not ProcessEntry(tn, elSub) Then
                        parentTN.Nodes.Add(tn)
                        Return False
                    Else
                        parentTN.Nodes.Add(tn)
                        If Not EBMLItems.Values(elSub).category = EBMLItems.Values(elSub + 1).category Then
                            ' we changed categories
                            Return True
                        End If
                    End If
                End If
            End If
            elSub += 1
        End While
        parentTN.Nodes.Add(tn)

        Return False
    End Function
    Private Function ReadRow(xr As XmlTextReader, ByRef el As EBMLItem, level As String) As Boolean
        Dim s As String

        el.name = ReadTD(xr)
        s = ReadTD(xr) ' level
        If s = "g" Then
            el.level = 0
        ElseIf Not IsNumeric(s) Then
            Return False
        Else
            el.level = CInt(s)
        End If
        s = ReadTD(xr)
        el.ID = s.Replace("[", "").Replace("]", "")
        s = ReadTD(xr)
        If s = "mand." Then
            el.mandatory = True
        Else
            el.mandatory = False
        End If
        ReadTD(xr)
        ReadTD(xr)
        s = ReadTD(xr)
        If s = "-" Then
            el.hasDefault = False
        Else
            el.hasDefault = True
        End If
        el.type = ReadAbbr(xr)
        If el.type = "" Then
            MsgBox("Abbr field invalid")
            Return False
        End If
        ReadTD(xr)
        ReadTD(xr)
        ReadTD(xr)
        ReadTD(xr)
        ReadTD(xr)
        el.description = ReadTD(xr)
        While xr.Read
            If xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "tr" Then
                Return True
            End If
        End While
        Return False
    End Function
    Private Function ReadTD(xr As XmlTextReader) As String
        Dim sb As New StringBuilder

        While xr.Read
            If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "td" Then
                xr.Read()
                If xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "td" Then
                    Return ""
                Else
                    sb.Append(xr.Value)
                End If
            ElseIf xr.NodeType = XmlNodeType.Text Then
                sb.Append(xr.Value)
            ElseIf xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "td" Then
                Return sb.ToString
            End If
        End While
        Return Nothing
    End Function
    Private Function ReadAbbr(xr As XmlTextReader) As String
        Dim value As String = ""

        While xr.Read
            If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "abbr" Then
                xr.Read()
                value = xr.Value
            ElseIf xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "td" Then
                Return value
            End If
        End While
        Return Nothing
    End Function
    Private Sub tvItems_AfterSelect(sender As System.Object, e As System.Windows.Forms.TreeViewEventArgs) Handles tvItems.AfterSelect
        Dim tn As TreeNode
        Dim el As EBMLItem

        If Not tvItems.SelectedNode Is Nothing Then
            tn = tvItems.SelectedNode
            el = GetEBMLElementByName(tn.Text)
            DecodeEBML(el)
        End If
    End Sub
    Private Sub DecodeEBML(el As EBMLItem)
        If el.name = "" Then
            lblName.Text = ""
            lblID.Text = ""
            lblCategory.Text = ""
            lblLevel.Text = ""
            lblDataType.Text = ""
            lblMandatory.Text = ""
            tbDescription.Text = ""
        Else
            lblName.Text = el.name
            lblID.Text = el.ID
            lblCategory.Text = el.category
            If el.level = -1 Then
                lblLevel.Text = "g"
            Else
                lblLevel.Text = el.level
            End If
            lblDataType.Text = DataTypeCharToString(el.type)
            If el.mandatory Then
                lblMandatory.Text = "True"
            Else
                lblMandatory.Text = "False"
            End If
            tbDescription.Text = el.description
        End If
    End Sub
    Private Function GetEBMLElementByName(name As String) As EBMLItem
        For Each el As EBMLItem In EBMLItems.Values
            If el.name = name Then
                Return el
            End If
        Next
        Return Nothing
    End Function
    Private Function GetEBMLElementByID(ID As String) As EBMLItem
        Dim el As EBMLItem

        For Each el In EBMLItems.Values
            If el.ID = ID Then
                Return el
            End If
        Next
        el = New EBMLItem
        el.name = ""
        Return el
    End Function
    Private Function GetTNByName(tnc As TreeNodeCollection, name As String) As Boolean
        For Each tn As TreeNode In tnc
            If tn.Text = name Then
                tvItems.SelectedNode = tn
                tvItems.Focus()
                Return True
            Else
                If GetTNByName(tn.Nodes, name) Then
                    Return True
                End If
            End If
        Next
        Return False
    End Function
    Private Function GetTNByID(tnc As TreeNodeCollection, ID As String) As Boolean
        Dim el As EBMLItem

        For Each tn As TreeNode In tnc
            el = GetEBMLElementByName(tn.Text)
            If el.ID = ID Then
                tvItems.SelectedNode = tn
                tvItems.Focus()
                Return True
            Else
                If tn.Nodes.Count > 0 Then
                    If GetTNByID(tn.Nodes, ID) Then
                        Return True
                    End If
                End If
            End If
        Next
        Return False
    End Function
    Private Function DataTypeCharToString(ch As Char) As String
        For Each dt As DataType In dataTypes
            If ch = dt.chType Then
                Return dt.description
            End If
        Next
        MsgBox(ch & " is not a valid datatype character")
        Return "Invalid"
    End Function
End Class
