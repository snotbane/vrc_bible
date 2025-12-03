
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BibleHost : UdonSharpBehaviour
{
	#region Constants

	internal const int LUT_REF_LENGTH = 9;
	internal const int LUT_REF_PREFIX_LENGTH = 10;
	internal const char SEP = '\n';

	#endregion
	#region Pinions

	/** <<============================================================>> **/

	[Header("References")]

	public TranslationButton trans_default;

	[SerializeField] private BibleReader _reader;
	public BibleReader reader => _reader;

	[SerializeField] private WindowSelectorButton _trans_button;
	[SerializeField] private WindowSelectorButton _book_button;
	[SerializeField] private WindowSelectorButton _chapter_button;

	[SerializeField] private TabSelector _tab_selector;

	[SerializeField] private ButtonGrid _book_selector;
	public ButtonGrid book_selector => _book_selector;
	[SerializeField] private ButtonGrid _chapter_selector;
	public ButtonGrid chapter_selector => _chapter_selector;

	[Header("Settings")]

	private int _chapter_index = 0;
	[UdonSynced] private int _chapter_index_SYNC = 0;
	public int chapter_index
	{
		get => _chapter_index;
		set
		{
			if (value == _chapter_index_SYNC) return;
			_chapter_index_SYNC = value;
			UpdateChapterIndex();

			Networking.SetOwner(Networking.LocalPlayer, gameObject);
			RequestSerialization();
		}
	}
	private void UpdateChapterIndex()
	{
		// if (_chapter_index == _chapter_index_SYNC) return;
		_chapter_index = _chapter_index_SYNC;

		_chapter_button.label.text = $"{chapter_locals[_chapter_index] + 1}";
		_book_button.label.text = $"{book_names[chapter_books[_chapter_index]]}";
		_book_button.RewriteLabel(_button_font_prefab);
	}
	public int book_index => chapter_books[_chapter_index];

	public EBibleWindow active_window_index
	{
		get => (EBibleWindow)_tab_selector.current_index;
		set => _tab_selector.current_index = (int)value;
	}
	private string _translation_text;
	internal string translation_text { get => _translation_text; private set => _translation_text = value; }

	private GameObject _button_font_prefab;
	public GameObject button_font_prefab => _button_font_prefab;
	private GameObject _reader_font_prefab;
	public GameObject reader_font_prefab => _reader_font_prefab;

	/// <summary>
	/// Total number books in the translation.
	///</summary>
	internal int max_book_count = 66;

	/// <summary>
	/// Total number of chapters in the translation. Used to simplify reference indeces and quicken verse loading.
	///</summary>
	internal int max_chapter_count = 1189;

	// Full names of each book, e.g. Genesis, Exodus, Leviticus
	internal string[] book_names;
	// Abbreviated names of each book, e.g. GEN, EXO, LEV
	internal string[] book_names_short;
	// The length (in chapters) of each book.
	internal int[] book_lengths;
	// The head chapter address (in the global chapter index) of each book.
	internal int[] book_heads;

	// The chapter number each chapter is, relative to the book it's in.
	internal int[] chapter_locals;
	// The book each chapter is located in (this array has a lot of repitition).
	internal int[] chapter_books;
	// The number of verses in each chapter.
	internal int[] chapter_lengths;
	// I forgor
	internal int[] chapter_heads;

	#endregion

	void Start()
	{
// This precomp directive reduces load times when playing in editor. To test the Bible in editor, select a translation before clicking any other buttons.
#if !UNITY_EDITOR
		trans_default.UpdateHost();
#endif
	}

	public void Despawn()
	{
		chapter_index = 0;
		active_window_index = EBibleWindow.BookSelector;
	}

	public override void OnDeserialization()
	{
		UpdateChapterIndex();
	}

	public void Init(string abbr, TextAsset content_file, GameObject __button_font_prefab, GameObject __reader_font_prefab)
	{
		_button_font_prefab = __button_font_prefab;
		_reader_font_prefab = __reader_font_prefab;

		translation_text = content_file.text;

		/** Set the number of all books in the translation. Only count the header section.
		*/
		max_book_count = -1;
		var cursor = 0;
		while (translation_text[cursor] != '0')
		{
			cursor = BibleUtils.NthIndexOf(translation_text, SEP, 0, cursor);
			max_book_count += 1;
			// Debug.Log($"{max_book_count}: '{translation_text[cursor]}'");
			// Debug.Log($"{translation_text[cursor] != '0'}");
		}


		book_names = new string[max_book_count];
		book_names_short = new string[max_book_count];
		book_lengths = new int[max_book_count];
		book_heads = new int[max_book_count];

		/**	Set the number of all chapters in each book, and all book data.
		*/
		max_chapter_count = 0;
		for (var i = 0; i < max_book_count; i++)
		{
			var line_start = BibleUtils.NthIndexOf(translation_text, SEP, i - 1);
			var line_end = BibleUtils.NthIndexOf(translation_text, SEP, i) - 1;

			var line = translation_text.Substring(line_start, line_end - line_start);

			var name_tail = BibleUtils.NthIndexOf(line, ',', 0);
			var abbr_tail = BibleUtils.NthIndexOf(line, ',', 1);

			book_names[i] = line.Substring(0, name_tail - 1);
			book_names_short[i] = line.Substring(name_tail, abbr_tail - name_tail - 1);
			book_lengths[i] = int.Parse(line.Substring(abbr_tail));
			book_heads[i] = max_chapter_count;

			max_chapter_count += book_lengths[i];

			// Debug.Log($"Book: [{i}] {book_names[i]}, chapter length: {book_lengths[i]}, head: {book_heads[i]}");
		}


		chapter_locals = new int[max_chapter_count];
		chapter_books = new int[max_chapter_count];
		chapter_lengths = new int[max_chapter_count];
		chapter_heads = new int[max_chapter_count];

		translation_text = translation_text.Substring(cursor);

		/**	Set indeces for each chapter.
		*/
		cursor = 0;
		var b = 0;
		var l = 0;
		var v = 0;
		var h = 0;
		for (var i = 0; i < chapter_heads.Length; i++)
		{
			var head_address = GetAddress(cursor);
			var j = 0;
			do {
				cursor = translation_text.IndexOf(SEP, cursor) + 1;
				j += 1;
			} while (AddressesShareChapter(head_address, GetAddress(cursor)));

			chapter_books[i] = b;
			chapter_locals[i] = l;
			chapter_lengths[i] = j;
			chapter_heads[i] = h + LUT_REF_PREFIX_LENGTH;

			// Debug.Log($"[{i}] book={b}, local={l}, length={j}, head={h}, head_address={head_address}, text={CreateChapterText(i)}");

			h = BibleUtils.NthIndexOf(translation_text, SEP, chapter_lengths[i] - 1, h);
			v += j;
			if (AddressesShareBook(head_address, GetAddress(cursor)))
				l++;
			else
			{
				l = 0;
				b++;
			}
		}

		if (_trans_button.label == null) _trans_button.Start(); _chapter_button.Start(); _book_button.Start();

		_trans_button.label.text = abbr;

		_book_selector.ResetChildren();
		_book_selector.UpdateLabels(_button_font_prefab);

		_chapter_selector.ResetChildren();

		UpdateChapterIndex();
		reader.Init(_reader_font_prefab);
	}

	public string CreateChapterText(int chapter)
	{
		var result = string.Empty;

		var char_head = chapter_heads[chapter];
		for (var i = 0; i < chapter_lengths[chapter]; i++)
		{
			var char_end = translation_text.IndexOf(SEP, char_head) - 1;
			result += $"{GetRichVerseNumber(i)}{translation_text.Substring(char_head, char_end - char_head)} ";
			char_head = char_end + LUT_REF_PREFIX_LENGTH + 2;
		}

		return result;
	}

	private static string GetRichVerseNumber(int index) => $"<sup>{index + 1}</sup>";

	/** <<============================================================>> **/

	public int GetChapterFromLocals(int book, int chapter) => book_heads[book] + chapter;
	public string GetAddress(int i) => translation_text.Substring(i, LUT_REF_LENGTH);

	private static bool AddressesShareBook(string a, string b) => a.Substring(0, 3) == b.Substring(0, 3);
	private static bool AddressesShareChapter(string a, string b) => a.Substring(0, 6) == b.Substring(0, 6);
}

public enum EBibleWindow
{
	Settings,
	TransSelector,
	BookSelector,
	ChapterSelector,
	Reader,
}
