<?xml version="1.0" encoding="utf-8"?>
<doc>
  <assembly>
    <name>System.Runtime.CompilerServices.Unsafe</name>
  </assembly>
  <members>
    <member name="T:System.Runtime.CompilerServices.Unsafe">
      <summary>Contains generic, low-level functionality for manipulating pointers.</summary>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Add``1(``0@,System.Int32)">
      <summary>Adds an element offset to the given reference.</summary>
      <param name="source">The reference to add the offset to.</param>
      <param name="elementOffset">The offset to add.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A new reference that reflects the addition of offset to pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Add``1(``0@,System.IntPtr)">
      <summary>Adds an element offset to the given reference.</summary>
      <param name="source">The reference to add the offset to.</param>
      <param name="elementOffset">The offset to add.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A new reference that reflects the addition of offset to pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Add``1(``0@,System.UIntPtr)">
      <summary>Adds an element offset to the given reference.</summary>
      <param name="source">The reference to add the offset to.</param>
      <param name="elementOffset">The offset to add.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A new reference that reflects the addition of offset to pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Add``1(System.Void*,System.Int32)">
      <summary>Adds an element offset to the given void pointer.</summary>
      <param name="source">The void pointer to add the offset to.</param>
      <param name="elementOffset">The offset to add.</param>
      <typeparam name="T">The type of void pointer.</typeparam>
      <returns>A new void pointer that reflects the addition of offset to the specified pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AddByteOffset``1(``0@,System.IntPtr)">
      <summary>Adds a byte offset to the given reference.</summary>
      <param name="source">The reference to add the offset to.</param>
      <param name="byteOffset">The offset to add.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A new reference that reflects the addition of byte offset to pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AddByteOffset``1(``0@,System.UIntPtr)">
      <summary>Adds a byte offset to the given reference.</summary>
      <param name="source">The reference to add the offset to.</param>
      <param name="byteOffset">The offset to add.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A new reference that reflects the addition of byte offset to pointer.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AreSame``1(``0@,``0@)">
      <summary>Determines whether the specified references point to the same location.</summary>
      <param name="left">The first reference to compare.</param>
      <param name="right">The second reference to compare.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>
        <see langword="true" /> if <paramref name="left" /> and <paramref name="right" /> point to the same location; otherwise, <see langword="false" />.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.As``1(System.Object)">
      <summary>Casts the given object to the specified type.</summary>
      <param name="o">The object to cast.</param>
      <typeparam name="T">The type which the object will be cast to.</typeparam>
      <returns>The original object, casted to the given type.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.As``2(``0@)">
      <summary>Reinterprets the given reference as a reference to a value of type <typeparamref name="TTo" />.</summary>
      <param name="source">The reference to reinterpret.</param>
      <typeparam name="TFrom">The type of reference to reinterpret.</typeparam>
      <typeparam name="TTo">The desired type of the reference.</typeparam>
      <returns>A reference to a value of type <typeparamref name="TTo" />.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AsPointer``1(``0@)">
      <summary>Returns a pointer to the given by-ref parameter.</summary>
      <param name="value">The object whose pointer is obtained.</param>
      <typeparam name="T">The type of object.</typeparam>
      <returns>A pointer to the given value.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AsRef``1(``0@)">
      <summary>Reinterprets the given read-only reference as a reference.</summary>
      <param name="source">The read-only reference to reinterpret.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>A reference to a value of type <typeparamref name="T" />.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.AsRef``1(System.Void*)">
      <summary>Reinterprets the given location as a reference to a value of type <typeparamref name="T" />.</summary>
      <param name="source">The location of the value to reference.</param>
      <typeparam name="T">The type of the interpreted location.</typeparam>
      <returns>A reference to a value of type <typeparamref name="T" />.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.ByteOffset``1(``0@,``0@)">
      <summary>Determines the byte offset from origin to target from the given references.</summary>
      <param name="origin">The reference to origin.</param>
      <param name="target">The reference to target.</param>
      <typeparam name="T">The type of reference.</typeparam>
      <returns>Byte offset from origin to target i.e. <paramref name="target" /> - <paramref name="origin" />.</returns>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Copy``1(``0@,System.Void*)">
      <summary>Copies a value of type <typeparamref name="T" /> to the given location.</summary>
      <param name="destination">The location to copy to.</param>
      <param name="source">A pointer to the value to copy.</param>
      <typeparam name="T">The type of value to copy.</typeparam>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.Copy``1(System.Void*,``0@)">
      <summary>Copies a value of type <typeparamref name="T" /> to the given location.</summary>
      <param name="destination">The location to copy to.</param>
      <param name="source">A reference to the value to copy.</param>
      <typeparam name="T">The type of value to copy.</typeparam>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.CopyBlock(System.Byte@,System.Byte@,System.UInt32)">
      <summary>Copies bytes from the source address to the destination address.</summary>
      <param name="destination">The destination address to copy to.</param>
      <param name="source">The source address to copy from.</param>
      <param name="byteCount">The number of bytes to copy.</param>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.CopyBlock(System.Void*,System.Void*,System.UInt32)">
      <summary>Copies bytes from the source address to the destination address.</summary>
      <param name="destination">The destination address to copy to.</param>
      <param name="source">The source address to copy from.</param>
      <param name="byteCount">The number of bytes to copy.</param>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(System.Byte@,System.Byte@,System.UInt32)">
      <summary>Copies bytes from the source address to the destination address without assuming architecture dependent alignment of the addresses.</summary>
      <param name="destination">The destination address to copy to.</param>
      <param name="source">The source address to copy from.</param>
      <param name="byteCount">The number of bytes to copy.</param>
    </member>
    <member name="M:System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(System.Void*,System.Void*,System.UInt32)">
      <summary>Copies bytes