/*
This file is part of the iText (R) project.
Copyright (c) 1998-2024 Apryse Group NV
Authors: Apryse Software.

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://itextpdf.com/sales.  For AGPL licensing, see below.

AGPL licensing:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace iText.Svg.Renderers.Path.Impl {
    /// <summary>Implements shorthand/smooth quadraticCurveTo (T) attribute of SVG's path element</summary>
    public class QuadraticSmoothCurveTo : QuadraticCurveTo {
        internal const int ARGUMENT_SIZE = 2;

        public QuadraticSmoothCurveTo()
            : this(false) {
        }

        public QuadraticSmoothCurveTo(bool relative)
            : base(relative, new SmoothOperatorConverter()) {
        }
    }
}
